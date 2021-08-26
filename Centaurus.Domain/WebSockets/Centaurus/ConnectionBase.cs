using Centaurus.Domain;
using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.Xdr;
using NLog;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    public enum ConnectionState
    {
        /// <summary>
        /// Connection established.
        /// </summary>
        Connected = 0,
        /// <summary>
        /// Ready to receive and send messages.
        /// </summary>
        Ready = 1,
        /// <summary>
        /// Connection was closed.
        /// </summary>
        Closed = 3
    }

    public abstract class ConnectionBase : ContextualBase, IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        protected readonly WebSocket webSocket;
        public ConnectionBase(Domain.ExecutionContext context, KeyPair pubKey, WebSocket webSocket)
            : base(context)
        {
            this.webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));

            PubKey = pubKey ?? throw new ArgumentNullException(nameof(pubKey));
            PubKeyAddress = PubKey.ToString();

            incommingBuffer = XdrBufferFactory.Rent(inBufferSize);
            outgoingBuffer = XdrBufferFactory.Rent(outBufferSize);

            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }

        /// <summary>
        /// Current connection public key
        /// </summary>
        public RawPubKey PubKey { get; }

        public string PubKeyAddress { get; }

        protected virtual int inBufferSize { get; } = 1024;
        protected virtual int outBufferSize { get; } = 64 * 1024;

        protected XdrBufferFactory.RentedBuffer incommingBuffer;
        protected XdrBufferFactory.RentedBuffer outgoingBuffer;

        public bool IsAuditor => this is IAuditorConnection;

        ConnectionState connectionState;
        /// <summary>
        /// Current connection state
        /// </summary>
        public ConnectionState ConnectionState
        {
            get
            {
                return connectionState;
            }
            set
            {
                if (connectionState != value)
                {
                    var prevValue = connectionState;
                    connectionState = value;
                    logger.Trace($"Connection {PubKeyAddress} is in {connectionState} state. Prev state is {prevValue}.");
                    OnConnectionStateChanged?.Invoke((this, prevValue, connectionState));

                    if (value == ConnectionState.Ready && prevValue == ConnectionState.Connected) //prevent multiple invocation
                        Context.ExtensionsManager.ConnectionReady(this);
                }
            }
        }

        public event Action<(ConnectionBase connection, ConnectionState prev, ConnectionState current)> OnConnectionStateChanged;

        protected readonly CancellationTokenSource cancellationTokenSource;
        protected readonly CancellationToken cancellationToken;

        public async Task CloseConnection(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string desc = null)
        {
            try
            {
                Context.ExtensionsManager.BeforeConnectionClose(this);
                if (webSocket.State == WebSocketState.Open)
                {
                    desc = desc ?? status.ToString();
                    logger.Trace($"Connection with {PubKeyAddress} is closed. Status: {status}, description: {desc}");

                    await sendMessageSemaphore.WaitAsync();
                    try
                    {
                        cancellationTokenSource.Cancel();
                        var timeoutTokenSource = new CancellationTokenSource(1000);
                        await webSocket.CloseAsync(status, desc, timeoutTokenSource.Token);
                    }
                    catch (WebSocketException exc)
                    {
                        //ignore client disconnection
                        if (exc.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely
                            && exc.WebSocketErrorCode != WebSocketError.InvalidState)
                            throw;
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        sendMessageSemaphore?.Release();
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Error on close connection");
            }
        }

        public virtual async Task SendMessage(Message message)
        {
            var envelope = message.CreateEnvelope();
            await SendMessage(envelope);
        }

        private readonly SemaphoreSlim sendMessageSemaphore = new SemaphoreSlim(1);

        public virtual async Task SendMessage(MessageEnvelopeBase envelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            await sendMessageSemaphore.WaitAsync();
            try
            {
                envelope.Sign(Context.Settings.KeyPair, outgoingBuffer.Buffer);

                Context.ExtensionsManager.BeforeSendMessage(this, envelope);

                logger.Trace($"Connection {PubKeyAddress}, about to send {envelope.Message.GetMessageType()} message.");

                using (var writer = new XdrBufferWriter(outgoingBuffer.Buffer))
                {
                    XdrConverter.Serialize(envelope, writer);
                    if (webSocket == null)
                        throw new ObjectDisposedException(nameof(webSocket));
                    if (webSocket.State == WebSocketState.Open)
                        await webSocket.SendAsync(outgoingBuffer.Buffer.AsMemory(0, writer.Length), WebSocketMessageType.Binary, true, cancellationToken);
                    Context.ExtensionsManager.AfterSendMessage(this, envelope);

                    logger.Trace($"Connection {PubKeyAddress}, message {envelope.Message.GetMessageType()} sent. Size: {writer.Length}");
                }
            }
            catch (Exception exc)
            {
                if (exc is OperationCanceledException
                    || exc is WebSocketException socketException && (socketException.WebSocketErrorCode == WebSocketError.InvalidState))
                    return;
                Context.ExtensionsManager.SendMessageFailed(this, envelope, exc);
                throw;
            }
            finally
            {
                sendMessageSemaphore.Release();
            }
        }

        public async Task Listen()
        {
            try
            {
                while (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted && !cancellationToken.IsCancellationRequested)
                {
                    //if connection isn't validated yet 256 bytes of max message is enough for handling handshake response
                    var maxLength = ConnectionState == ConnectionState.Connected ? WebSocketExtension.ChunkSize : 0;
                    var messageType = await webSocket.GetWebsocketBuffer(incommingBuffer, cancellationToken, maxLength);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        //the client send close message
                        if (messageType == WebSocketMessageType.Close)
                        {
                            if (webSocket.State != WebSocketState.CloseReceived)
                                continue;
                            await sendMessageSemaphore.WaitAsync();
                            try
                            {
                                var timeoutTokenSource = new CancellationTokenSource(1000);
                                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Close", CancellationToken.None);
                            }
                            finally
                            {
                                sendMessageSemaphore.Release();
                                cancellationTokenSource.Cancel();
                            }
                        }
                        else
                        {
                            MessageEnvelopeBase envelope = null;
                            try
                            {
                                var reader = new XdrBufferReader(incommingBuffer.Buffer, incommingBuffer.Length);
                                envelope = XdrConverter.Deserialize<MessageEnvelopeBase>(reader);

                                logger.Trace($"Connection {PubKeyAddress}, message {envelope.Message.GetMessageType()} received.");
                                if (!await HandleMessage(envelope))
                                    throw new UnexpectedMessageException($"No handler registered for message type {envelope.Message.GetMessageType()}.");
                                logger.Trace($"Connection {PubKeyAddress}, message {envelope.Message.GetMessageType()} handled.");
                            }
                            catch (BaseClientException exc)
                            {
                                Context.ExtensionsManager.HandleMessageFailed(this, envelope, exc);

                                var statusCode = exc.GetStatusCode();

                                //prevent recursive error sending
                                if (!IsAuditor && !(envelope == null || envelope.Message is ResultMessage))
                                    _ = SendMessage(envelope.CreateResult(statusCode).CreateEnvelope<MessageEnvelopeSigneless>());
                                if (statusCode == ResultStatusCodes.InternalError || !Context.IsAlpha)
                                    logger.Error(exc);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Context.ExtensionsManager.HandleMessageFailed(this, null, e);
                var closureStatus = WebSocketCloseStatus.InternalServerError;
                var desc = default(string);
                if (e is WebSocketException webSocketException
                    && webSocketException.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) //connection already closed by the other side
                    return;
                else if (e is ConnectionCloseException closeException)
                {
                    closureStatus = closeException.Status;
                    desc = closeException.Description;
                }
                else if (e is System.FormatException)
                    closureStatus = WebSocketCloseStatus.ProtocolError;
                else
                    logger.Error(e);
                await CloseConnection(closureStatus, desc);
            }
            finally
            {
                ConnectionState = ConnectionState.Closed;
            }
        }

        private async Task<bool> HandleMessage(MessageEnvelopeBase envelope)
        {
            return await Context.MessageHandlers.HandleMessage(this, envelope.ToIncomingMessage(incommingBuffer));
        }

        bool isDisposed = false;

        public virtual void Dispose()
        {
            if (isDisposed)
                throw new ObjectDisposedException("Connection already disposed.");

            Thread.Sleep(100); //wait all tasks to exit

            sendMessageSemaphore.Dispose();

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();

            //make sure the socket is closed
            if (webSocket.State != WebSocketState.Closed)
                webSocket.Abort();

            incommingBuffer?.Dispose();
            incommingBuffer = null;

            outgoingBuffer?.Dispose();
            outgoingBuffer = null;

            isDisposed = true;
        }
    }
}