using NLog;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Xdr;

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

    public abstract class BaseWebSocketConnection : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        protected WebSocket webSocket;
        public BaseWebSocketConnection(WebSocket webSocket, string ip)
        {
            this.webSocket = webSocket;
            this.Ip = ip;

            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }
        public string Ip { get; }

        /// <summary>
        /// Current connection public key
        /// </summary>
        public RawPubKey ClientPubKey { get; set; }

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
                    connectionState = value;
                    OnConnectionStateChanged?.Invoke(this, connectionState);
                }
            }
        }

        protected CancellationTokenSource cancellationTokenSource;
        protected CancellationToken cancellationToken;

        public event EventHandler<ConnectionState> OnConnectionStateChanged;

        public async Task CloseConnection(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string desc = null)
        {
            if (webSocket == null)
                return;
            try
            {
                Global.ExtensionsManager.BeforeConnectionClose(this);
                if (webSocket.State == WebSocketState.Open
                    || webSocket.State == WebSocketState.CloseReceived
                    || webSocket.State == WebSocketState.CloseSent)
                {
                    var connectionPubKey = "no public key";
                    if (ClientPubKey != null)
                        connectionPubKey = $"public key {ClientPubKey}";
                    desc = desc ?? status.ToString();
                    logger.Trace($"Connection with {connectionPubKey} is closed. Status: {status}, description: {desc}");

                    try
                    {
                        cancellationTokenSource?.Cancel();
                        await webSocket.CloseAsync(status, desc, CancellationToken.None);
                    }
                    catch (WebSocketException exc)
                    {
                        //ignore client disconnection
                        if (exc.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely
                            && exc.WebSocketErrorCode != WebSocketError.InvalidState)
                            throw;
                    }
                    catch (OperationCanceledException) { }
                }
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
            finally
            {
                Dispose();
                ConnectionState = ConnectionState.Closed;
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
            await sendMessageSemaphore.WaitAsync();
            try
            {
                Global.ExtensionsManager.BeforeSendMessage(this, envelope);
                if (!envelope.IsSignedBy(Global.Settings.KeyPair.PublicKey))
                    envelope.Sign(Global.Settings.KeyPair);

                using var buffer = XdrBufferFactory.Rent();
                using var writer = new XdrBufferWriter(buffer.Buffer);
                XdrConverter.Serialize(envelope, writer);

                if (webSocket == null)
                    throw new ObjectDisposedException(nameof(webSocket));
                await webSocket.SendAsync(buffer.AsSegment(0, writer.Length), WebSocketMessageType.Binary, true, cancellationToken);
                Global.ExtensionsManager.AfterSendMessage(this, envelope);
            }
            catch (Exception exc)
            {
                if (exc is OperationCanceledException
                    || exc is WebSocketException socketException && (socketException.WebSocketErrorCode == WebSocketError.InvalidState))
                    return;
                Global.ExtensionsManager.SendMessageFailed(this, envelope, exc);
                throw;
            }
            finally
            {
                sendMessageSemaphore.Release();
            }
        }


        private SemaphoreSlim receiveMessageSemaphore = new SemaphoreSlim(1);

        public async Task Listen()
        {
            await receiveMessageSemaphore.WaitAsync();
            try
            {
                do
                {
                    using var buffer = await webSocket.GetWebsocketBuffer(cancellationToken);
                    MessageEnvelope envelope = null;
                    try
                    {
                        var reader = new XdrBufferReader(buffer.Buffer, buffer.Length);
                        envelope = XdrConverter.Deserialize<MessageEnvelope>(reader);

                        if (!await HandleMessage(envelope))
                            throw new UnexpectedMessageException($"No handler registered for message type {envelope.Message.MessageType}.");
                    }
                    catch (Exception exc)
                    {
                        if (exc is ConnectionCloseException)
                            throw;

                        Global.ExtensionsManager.HandleMessageFailed(this, envelope, exc);

                        var statusCode = exc.GetStatusCode();

                        //prevent recursive error sending
                        if (!(envelope == null || envelope.Message is ResultMessage))
                            _ = SendMessage(envelope.CreateResult(statusCode));
                        if (statusCode == ResultStatusCodes.InternalError || !Global.IsAlpha)
                            logger.Error(exc);
                    }
                }
                while (true);
            }
            catch (ConnectionCloseException e)
            {
                await CloseConnection(e.Status, e.Description);
            }
            catch (Exception e)
            {
                if (e is OperationCanceledException)
                    return;

                Global.ExtensionsManager.HandleMessageFailed(this, null, e);
                var closureStatus = WebSocketCloseStatus.InternalServerError;
                if (e is WebSocketException webSocketException && webSocketException.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                    closureStatus = WebSocketCloseStatus.NormalClosure;
                else if (e is FormatException)
                    closureStatus = WebSocketCloseStatus.ProtocolError;
                else
                    logger.Error(e);
                await CloseConnection(closureStatus);
            }
            finally
            {
                receiveMessageSemaphore.Release();
            }
        }

        protected abstract Task<bool> HandleMessage(MessageEnvelope envelope);

        public virtual void Dispose()
        {
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            webSocket?.Dispose();
            webSocket = null;
        }
    }
}
