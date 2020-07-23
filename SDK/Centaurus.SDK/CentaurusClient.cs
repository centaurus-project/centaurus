﻿using Centaurus.Models;
using Centaurus.Xdr;
using stellar_dotnet_sdk;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.SDK
{
    public class CentaurusClient
    {
        static CentaurusClient()
        {
            DynamicSerializersInitializer.Init();
        }

        private readonly SemaphoreSlim connectionSemaphore = new SemaphoreSlim(1);

        private CentaurusConnection connection;
        private Uri alphaWebSocketAddress;
        private KeyPair keyPair;

        private TaskCompletionSource<bool> handshakeResult = new TaskCompletionSource<bool>();

        public CentaurusClient(Uri alphaWebSocketAddress, KeyPair keyPair)
        {
            this.alphaWebSocketAddress = alphaWebSocketAddress ?? throw new ArgumentNullException(nameof(alphaWebSocketAddress));
            this.keyPair = keyPair ?? throw new ArgumentNullException(nameof(keyPair));
        }

        public async Task Connect()
        {
            try
            {
                await connectionSemaphore.WaitAsync();
                connection = new CentaurusConnection(alphaWebSocketAddress, keyPair);
                SubscribeToEvents(connection);
                await connection.EstablishConnection();

                await handshakeResult.Task;

                if (handshakeResult.Task.Result == false)
                    throw new Exception("Login failed.");
            }
            catch
            {
                UnsubscribeFromEvents(connection);
                throw;
            }
            finally
            {

                connectionSemaphore.Release();
            }
        }

        public void CloseConnection()
        {
            try
            {
                connectionSemaphore.Wait();
                if (connection != null)
                    connection.CloseConnection();
                connection = null;
            }
            finally
            {

                connectionSemaphore.Release();
            }
        }

        public event Action<MessageEnvelope> OnMessage;

        public event Action<Exception> OnException;

        public event Action OnConnected;

        public event Action<WebSocketCloseStatus> OnClosed;

        private void SubscribeToEvents(CentaurusConnection connection)
        {
            connection.OnClosed += Connection_OnClosed;
            connection.OnException += Connection_OnException;
            connection.OnMessage += Connection_OnMessage;
        }

        private void UnsubscribeFromEvents(CentaurusConnection connection)
        {
            if (connection == null)
                return;
            connection.OnClosed -= Connection_OnClosed;
            connection.OnException -= Connection_OnException;
            connection.OnMessage -= Connection_OnMessage;
        }

        private bool HandleHandshake(MessageEnvelope envelope)
        {
            if (envelope.Message is HandshakeInit)
            {
                try
                {
                    var resultTask = connection.SendMessage(envelope.Message.CreateEnvelope());
                    resultTask.Wait();
                    var result = (ResultMessage)resultTask.Result.Message;
                    if (result.Status != ResultStatusCodes.Success)
                        throw new Exception();
                    handshakeResult.SetResult(true);
                }
                catch
                {
                    handshakeResult.SetResult(false);
                }
                return true;
            }
            return false;
        }

        private void Connection_OnMessage(MessageEnvelope envelope)
        {
            if (HandleHandshake(envelope))
                OnMessage?.Invoke(envelope);
        }

        private void Connection_OnException(Exception exception)
        {
            OnException?.Invoke(exception);
        }

        private void Connection_OnClosed(WebSocketCloseStatus status)
        {
            OnClosed?.Invoke(status);
        }

        private class CentaurusConnection : IDisposable
        {
            public CentaurusConnection(Uri _websocketAddress, KeyPair _clientKeyPair)
            {
                clientKeyPair = _clientKeyPair ?? throw new ArgumentNullException(nameof(_clientKeyPair));
                websocketAddress = new Uri(_websocketAddress ?? throw new ArgumentNullException(nameof(_websocketAddress)), "centaurus");

                //we don't need to create and sign heartbeat message on every sending
                hearbeatMessage = new Heartbeat().CreateEnvelope();
                hearbeatMessage.Sign(_clientKeyPair);
#if !DEBUG
                InitTimer();
#endif
            }

            public event Action<MessageEnvelope> OnMessage;

            public event Action<Exception> OnException;

            public event Action<WebSocketCloseStatus> OnClosed;

            public async Task EstablishConnection()
            {
                await webSocket.ConnectAsync(websocketAddress, CancellationToken.None);
                _ = Listen();
            }

            public void CloseConnection(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string desc = null)
            {
                var _webSocket = webSocket;
                if (_webSocket == null)
                    return;

                lock (_webSocket)
                {
                    if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting)
                        webSocket.Abort();
                    webSocket.Dispose();
                    webSocket = null;
                    var allPendingRequests = Requests.Values;
                    var closeException = new ConnectionCloseException(status, desc);
                    foreach (var pendingRequest in allPendingRequests)
                        pendingRequest.SetException(closeException);

                    if (status != WebSocketCloseStatus.NormalClosure)
                        _ = Task.Factory.StartNew(() => OnException?.Invoke(closeException));

                    OnClosed?.Invoke(status);
                }
            }

            public void Dispose()
            {
                CloseConnection();
            }

            #region Private Members

            private readonly SemaphoreSlim requestsSemaphore = new SemaphoreSlim(1);

            private System.Timers.Timer heartbeatTimer = null;

            private void InitTimer()
            {
                heartbeatTimer = new System.Timers.Timer();
                heartbeatTimer.Interval = 5000;
                heartbeatTimer.AutoReset = false;
                heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
            }

            private KeyPair clientKeyPair;
            private Uri websocketAddress;
            private MessageEnvelope hearbeatMessage;

            private void HeartbeatTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
            {
                _ = SendMessage(hearbeatMessage);
            }

            private ClientWebSocket webSocket = new ClientWebSocket();

            private void AssignRequestId(MessageEnvelope envelope)
            {
                var request = envelope.Message as RequestMessage;
                if (request == null || request.MessageId != 0)
                    return;
                var currentTicks = DateTime.UtcNow.Ticks;
                request.RequestId = request is NonceRequestMessage ? currentTicks : -currentTicks;
            }

            private TaskCompletionSource<MessageEnvelope> RegisterRequest(MessageEnvelope envelope)
            {
                lock (Requests)
                {
                    var response = new TaskCompletionSource<MessageEnvelope>(new { createdAt = DateTime.UtcNow.Ticks });
                    if (!Requests.TryAdd(envelope.Message.MessageId, response))
                        throw new Exception("Unable to add request to pending requests.");
                    return response;
                }
            }

            private bool TryResolveRequest(MessageEnvelope envelope)
            {
                var resultMessage = envelope.Message as ResultMessage;
                if (resultMessage != null)
                    lock (Requests)
                    {
                        if (Requests.TryRemove(resultMessage.OriginalMessage.Message.MessageId, out var task))
                        {
                            if (resultMessage.Status == ResultStatusCodes.Success)
                                task.SetResult(envelope);
                            else
                                task.SetException(new RequestException(envelope));
                            return true;
                        }
                    }
                return false;
            }

            public async Task<MessageEnvelope> SendMessage(MessageEnvelope envelope, CancellationToken ct = default)
            {
                AssignRequestId(envelope);
                if (!envelope.IsSignedBy(clientKeyPair))
                    envelope.Sign(clientKeyPair);

                var serializedData = XdrConverter.Serialize(envelope);
                await webSocket.SendAsync(serializedData, WebSocketMessageType.Binary, true, (ct == default ? CancellationToken.None : ct));

                if (heartbeatTimer != null)
                    heartbeatTimer.Reset();

                if (envelope != hearbeatMessage
                    && envelope.Message.MessageId > 0)
                {
                    //return response task
                    var response = RegisterRequest(envelope);
                    return await response.Task;
                }

                return await Task.FromResult<MessageEnvelope>(null);
            }

            private MessageEnvelope DeserializeMessage(XdrReader reader)
            {
                try
                {
                    return XdrConverter.Deserialize<MessageEnvelope>(reader);
                }
                catch
                {
                    throw new UnexpectedMessageException("Unable to deserialize message.");
                }
            }

            private async Task Listen()
            {
                try
                {
                    var reader = await webSocket.GetInputStreamReader();
                    while (true)
                    {
                        try
                        {
                            HandleMessage(DeserializeMessage(reader));
                            reader = await webSocket.GetInputStreamReader();
                        }
                        catch (UnexpectedMessageException e)
                        {
                            _ = Task.Factory.StartNew(() => OnException?.Invoke(e));
                        }
                    }
                }
                catch (WebSocketException e)
                {
                    CloseConnection(WebSocketCloseStatus.ProtocolError, e.Message);
                }
                catch (ConnectionCloseException e)
                {
                    CloseConnection(e.Status, e.Description);
                }
                catch (Exception e)
                {
                    CloseConnection(WebSocketCloseStatus.InternalServerError, e.Message);
                }
            }

            private ConcurrentDictionary<long, TaskCompletionSource<MessageEnvelope>> Requests { get; } = new ConcurrentDictionary<long, TaskCompletionSource<MessageEnvelope>>();

            private void HandleMessage(MessageEnvelope envelope)
            {
                TryResolveRequest(envelope);
                _ = Task.Factory.StartNew(() => OnMessage?.Invoke(envelope));
            }

            #endregion
        }
    }
}
