using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
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
        public BaseWebSocketConnection(WebSocket webSocket, bool heartbeatEnabled = false)
        {
            this.webSocket = webSocket;
#if !DEBUG
            if (heartbeatEnabled)
                InitTimer();
#endif
        }

        private System.Timers.Timer invalidationTimer = null;

        //If we didn't receive message during specified interval, we should close connection
        private void InitTimer()
        {
            invalidationTimer = new System.Timers.Timer();
            invalidationTimer.Interval = 5000;
            invalidationTimer.AutoReset = false;
            invalidationTimer.Elapsed += InvalidationTimer_Elapsed;
        }

        private async void InvalidationTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            await CloseConnection(WebSocketCloseStatus.PolicyViolation, "Connection is inactive");
        }

        /// <summary>
        /// Current connection public key
        /// </summary>
        public RawPubKey ClientPubKey { get; internal set; }

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
            protected internal set
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
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting)
                    await webSocket.CloseAsync(status, desc ?? status.ToString(), CancellationToken.None);
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
            if (!envelope.IsSignedBy(Global.Settings.KeyPair.PublicKey))
                envelope.Sign(Global.Settings.KeyPair);

            var serializedData = XdrConverter.Serialize(envelope);
            await webSocket.SendAsync(serializedData, WebSocketMessageType.Binary, true, (ct == default ? CancellationToken.None : ct));
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

                        if (invalidationTimer != null)
                            invalidationTimer.Reset();
                    }
                    catch (Exception exc)
                    {
                        if (exc is ConnectionCloseException)
                            throw;

                        var statusCode = ClientExceptionHelper.GetExceptionStatusCode(exc);

                        //prevent recursive error sending
                        if (!(envelope.Message is ResultMessage))
                            _ = SendMessage(new ResultMessage { Status = statusCode, OriginalMessage = envelope });
                        if (statusCode == ResultStatusCodes.InternalError)
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
                logger.Error(e);
                await CloseConnection(WebSocketCloseStatus.InternalServerError);
            }
            catch (Exception e)
            {
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
