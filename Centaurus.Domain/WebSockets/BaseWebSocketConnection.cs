using NLog;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Xdr;
using System.Reflection;
using System.Diagnostics;
using stellar_dotnet_sdk;

namespace Centaurus
{
    public enum ConnectionState
    {
        /// <summary>
        /// Connection established.
        /// </summary>
        Connected = 0,
        /// <summary>
        /// Successful handshake.
        /// </summary>
        Validated = 1,
        /// <summary>
        /// Ready to receive and send messages.
        /// </summary>
        Ready = 2,
        /// <summary>
        /// Connection was closed.
        /// </summary>
        Closed = 3
    }

    public abstract class BaseWebSocketConnection : ContextualBase, IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        protected WebSocket webSocket;
        public BaseWebSocketConnection(Domain.ExecutionContext context, WebSocket webSocket, string ip, int inBufferSize, int outBufferSize)
            :base(context)
        {
            this.webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            Ip = ip;

            incommingBuffer = XdrBufferFactory.Rent(inBufferSize);
            outgoingBuffer = XdrBufferFactory.Rent(outBufferSize);

            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }

        public string Ip { get; }

        /// <summary>
        /// Current connection public key
        /// </summary>
        public RawPubKey ClientPubKey { get; set; }

        private string clientKPAccountId;
        public string ClientKPAccountId
        {
            get
            {
                if (clientKPAccountId == null)
                    if (ClientPubKey != null)
                    {
                        clientKPAccountId = ((KeyPair)ClientPubKey).AccountId;
                    }
                    else
                        return "n/a";
                return clientKPAccountId;
            }
        }

        protected XdrBufferFactory.RentedBuffer incommingBuffer;
        protected XdrBufferFactory.RentedBuffer outgoingBuffer;
        public bool IsResultRequired { get; protected set; } = true;

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
                    logger.Trace($"Connection {ClientKPAccountId} is in {connectionState} state. Prev state is {prevValue}.");
                    OnConnectionStateChanged?.Invoke((this, prevValue, connectionState));
                }
            }
        }

        protected CancellationTokenSource cancellationTokenSource;
        protected CancellationToken cancellationToken;

        public event Action<(BaseWebSocketConnection connection, ConnectionState prev, ConnectionState current)> OnConnectionStateChanged;

        public async Task CloseConnection(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string desc = null)
        {
            try
            {
                Context.ExtensionsManager.BeforeConnectionClose(this);
                if (webSocket.State == WebSocketState.Open)
                {
                    desc = desc ?? status.ToString();
                    logger.Trace($"Connection with {ClientKPAccountId} is closed. Status: {status}, description: {desc}");

                    await sendMessageSemaphore.WaitAsync();
                    try
                    {
                        cancellationTokenSource?.Cancel();
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

        private SemaphoreSlim sendMessageSemaphore = new SemaphoreSlim(1);

        public virtual async Task SendMessage(MessageEnvelope envelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            await sendMessageSemaphore.WaitAsync();
            try
            {
                Context.ExtensionsManager.BeforeSendMessage(this, envelope);
                if (!envelope.IsSignedBy(Context.Settings.KeyPair.PublicKey))
                    envelope.Sign(Context.Settings.KeyPair, outgoingBuffer.Buffer);

                logger.Trace($"Connection {ClientKPAccountId}, about to send {envelope.Message.MessageType} message.");

                using (var writer = new XdrBufferWriter(outgoingBuffer.Buffer))
                {
                    XdrConverter.Serialize(envelope, writer);
                    if (webSocket == null)
                        throw new ObjectDisposedException(nameof(webSocket));
                    if (webSocket.State == WebSocketState.Open)
                        await webSocket.SendAsync(outgoingBuffer.Buffer.AsMemory(0, writer.Length), WebSocketMessageType.Binary, true, cancellationToken);
                    Context.ExtensionsManager.AfterSendMessage(this, envelope);

                    logger.Trace($"Connection {ClientKPAccountId}, message {envelope.Message.MessageType} sent. Size: {writer.Length}");
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
                    var messageType = await webSocket.GetWebsocketBuffer(incommingBuffer, cancellationToken);
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
                                sendMessageSemaphore?.Release();
                                cancellationTokenSource?.Cancel();
                            }
                        }
                        else
                        {
                            MessageEnvelope envelope = null;
                            try
                            {
                                var reader = new XdrBufferReader(incommingBuffer.Buffer, incommingBuffer.Length);
                                envelope = XdrConverter.Deserialize<MessageEnvelope>(reader);

                                logger.Trace($"Connection {ClientKPAccountId}, message {envelope.Message.MessageType} received.");
                                if (!await HandleMessage(envelope))
                                    throw new UnexpectedMessageException($"No handler registered for message type {envelope.Message.MessageType}.");
                                logger.Trace($"Connection {ClientKPAccountId}, message {envelope.Message.MessageType} handled.");
                            }
                            catch (BaseClientException exc)
                            {
                                Context.ExtensionsManager.HandleMessageFailed(this, envelope, exc);

                                var statusCode = exc.GetStatusCode();

                                //prevent recursive error sending
                                if (IsResultRequired && !(envelope == null || envelope.Message is ResultMessage))
                                    _ = Task.Factory.StartNew(async () => await SendMessage(envelope.CreateResult(statusCode))).Unwrap();
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

        protected abstract Task<bool> HandleMessage(MessageEnvelope message);

        public virtual void Dispose()
        {
            Thread.Sleep(100); //wait all tasks to exit

            sendMessageSemaphore?.Dispose();
            sendMessageSemaphore = null;

            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            //make sure the socket is closed
            if (webSocket.State != WebSocketState.Closed)
                webSocket.Abort();

            incommingBuffer?.Dispose();
            incommingBuffer = null;

            outgoingBuffer?.Dispose();
            outgoingBuffer = null;
        }
    }

    public abstract class BaseWebSocketConnection<TContext> : BaseWebSocketConnection, IContextual<TContext>
        where TContext : Domain.ExecutionContext
    {
        public BaseWebSocketConnection(Domain.ExecutionContext context, WebSocket webSocket, string ip, int inBufferSize, int outBufferSize)
            :base(context, webSocket, ip, inBufferSize, outBufferSize)
        { }

        public new TContext Context => (TContext)base.Context;
    }
}
