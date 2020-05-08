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
        public BaseWebSocketConnection(WebSocket webSocket)
        {
            this.webSocket = webSocket;
        }

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

        public event EventHandler<ConnectionState> OnConnectionStateChanged;

        public async Task CloseConnection(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string desc = null)
        {
            if (webSocket == null)
                return;
            try
            {
                Global.ExtensionsManager.BeforeConnectionClose(this);
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting)
                {
                    var connectionPubKey = "no public key";
                    if (ClientPubKey != null)
                        connectionPubKey = $"public key {ClientPubKey.ToString()}";
                    desc = desc ?? status.ToString();
                    logger.Info($"Connection with {connectionPubKey} is closed. Status: {status.ToString()}, description: {desc}");

                    await webSocket.CloseAsync(status, desc, CancellationToken.None);
                }
                Dispose();
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
            finally
            {
                ConnectionState = ConnectionState.Closed;
            }
        }

        public virtual async Task SendMessage(Message message, CancellationToken ct = default)
        {
            var envelope = message.CreateEnvelope();
            await SendMessage(envelope, ct);
        }

        public virtual async Task SendMessage(MessageEnvelope envelope, CancellationToken ct = default)
        {
            try
            {
                Global.ExtensionsManager.BeforeSendMessage(this, envelope);
                if (!envelope.IsSignedBy(Global.Settings.KeyPair.PublicKey))
                    envelope.Sign(Global.Settings.KeyPair);

                var serializedData = XdrConverter.Serialize(envelope);
                await webSocket.SendAsync(serializedData, WebSocketMessageType.Binary, true, (ct == default ? CancellationToken.None : ct));
                Global.ExtensionsManager.AfterSendMessage(this, envelope);
            }
            catch(Exception exc)
            {
                Global.ExtensionsManager.SendMessageFailed(this, envelope, exc);
                throw;
            }
        }

        public async Task Listen()
        {
            try
            {
                var reader = await webSocket.GetInputStreamReader();
                while (true)
                {
                    MessageEnvelope envelope = null;
                    try
                    {
                        envelope = XdrConverter.Deserialize<MessageEnvelope>(reader);

                        if (!await HandleMessage(envelope))
                            throw new UnexpectedMessageException($"No handler registered for message type {envelope.Message.MessageType.ToString()}.");
                    }
                    catch (Exception exc)
                    {
                        if (exc is ConnectionCloseException)
                            throw;

                        Global.ExtensionsManager.HandleMessageFailed(this, envelope, exc);

                        var statusCode = ClientExceptionHelper.GetExceptionStatusCode(exc);

                        //prevent recursive error sending
                        if (!(envelope.Message is ResultMessage))
                            _ = SendMessage(new ResultMessage { Status = statusCode, OriginalMessage = envelope });
                        if (statusCode == ResultStatusCodes.InternalError || !Global.IsAlpha)
                            logger.Error(exc);
                    }
                    reader = await webSocket.GetInputStreamReader();
                }
            }
            catch (ConnectionCloseException e)
            {
                await CloseConnection(e.Status, e.Description);
            }
            catch (WebSocketException e)
            {
                Global.ExtensionsManager.HandleMessageFailed(this, null, e);
                logger.Error(e);
                await CloseConnection(WebSocketCloseStatus.InternalServerError);
            }
            catch (Exception e)
            {
                Global.ExtensionsManager.HandleMessageFailed(this, null, e);
                logger.Error(e);
            }
        }

        protected abstract Task<bool> HandleMessage(MessageEnvelope envelope);

        public virtual void Dispose()
        {
            webSocket?.Dispose();
            webSocket = null;
        }
    }
}
