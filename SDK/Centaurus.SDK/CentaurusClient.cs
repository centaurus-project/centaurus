using Centaurus.Models;
using Centaurus.SDK.Models;
using Centaurus.Xdr;
using NSec.Cryptography;
using stellar_dotnet_sdk;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.SDK
{
    public class CentaurusClient: IDisposable
    {

        public CentaurusClient(Uri alphaWebSocketAddress, KeyPair keyPair/*, ConstellationInfo constellationInfo*/)
        {
            this.alphaWebSocketAddress = alphaWebSocketAddress ?? throw new ArgumentNullException(nameof(alphaWebSocketAddress));
            this.keyPair = keyPair ?? throw new ArgumentNullException(nameof(keyPair));
            //this.constellationInfo = constellationInfo ?? throw new ArgumentNullException(nameof(constellationInfo));
        }

        public async Task Connect()
        {
            try
            {
                await connectionSemaphore.WaitAsync();

                connection = new CentaurusConnection(alphaWebSocketAddress, keyPair);
                SubscribeToEvents(connection);
                await connection.EstablishConnection();

                var connectionStartedAt = DateTime.UtcNow;
                while (!handshakeResult.Task.IsCompleted
                    && DateTime.UtcNow - connectionStartedAt < new TimeSpan(0, 0, 5))
                    await Task.Delay(100);

                if (!handshakeResult.Task.IsCompleted || handshakeResult.Task.Result == false)
                    throw new Exception("Login failed.");
            }
            catch
            {
                UnsubscribeFromEvents(connection);
                connection?.Dispose();
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
                    connection.Dispose();
                connection = null;
            }
            finally
            {

                connectionSemaphore.Release();
            }
        }

        public event Action<MessageEnvelope> OnMessage;

        public event Action<MessageEnvelope> OnSend;

        public event Action<Exception> OnException;

        public event Action<WebSocketCloseStatus> OnClosed;

        public async Task<AccountDataModel> GetAccountData()
        {
            var data = await connection.SendMessage(new AccountDataRequest().CreateEnvelope());
            var rawMessage = (AccountDataResponse)data.Message;
            return new AccountDataModel
            {
                Balances = rawMessage.Balances.Select(x => new BalanceModel { Amount = x.Amount, Asset = x.Asset, Liabilities = x.Liabilities }).ToList()
            };
        }

        public async Task<MessageEnvelope> MakePayment(KeyPair destination, long amount, int asset)
        {
            var paymentMessage = destination.AccountId == keyPair.AccountId ? (PaymentRequestBase)new WithdrawalRequest() : new PaymentRequest();
            paymentMessage.Amount = amount;
            paymentMessage.Asset = asset;
            paymentMessage.Destination = destination;

            var result = await connection.SendMessage(paymentMessage.CreateEnvelope());
            return result;
        }

        public void Dispose()
        {
            UnsubscribeFromEvents(connection);
            connection?.Dispose();
        }

        #region Private members

        static CentaurusClient()
        {
            DynamicSerializersInitializer.Init();
        }

        private readonly SemaphoreSlim connectionSemaphore = new SemaphoreSlim(1);

        private CentaurusConnection connection;
        private Uri alphaWebSocketAddress;
        private KeyPair keyPair;
        //private ConstellationInfo constellationInfo;
        private TaskCompletionSource<bool> handshakeResult = new TaskCompletionSource<bool>();

        private void SubscribeToEvents(CentaurusConnection connection)
        {
            connection.OnClosed += Connection_OnClosed;
            connection.OnException += Connection_OnException;
            connection.OnMessage += Connection_OnMessage;
            connection.OnSend += Connection_OnSend;
        }

        private void UnsubscribeFromEvents(CentaurusConnection connection)
        {
            if (connection == null)
                return;
            connection.OnClosed -= Connection_OnClosed;
            connection.OnException -= Connection_OnException;
            connection.OnMessage -= Connection_OnMessage;
            connection.OnSend -= Connection_OnSend;
        }

        private bool HandleHandshake(MessageEnvelope envelope)
        {
            if (envelope.Message is HandshakeInit)
            {
                isConnecting = true;
                try
                {
                    var resultTask = connection.SendMessage(envelope.Message.CreateEnvelope());
                    resultTask.Wait();
                    var result = (ResultMessage)resultTask.Result.Message;
                    if (result.Status != ResultStatusCodes.Success)
                        throw new Exception();
                    handshakeResult.SetResult(true);
                    isConnected = true;
                }
                catch
                {
                    handshakeResult.SetResult(false);
                }
                isConnecting = false;
                return true;
            }
            return false;
        }

        private bool isConnected = false;

        private bool isConnecting = false;

        private void Connection_OnSend(MessageEnvelope envelope)
        {
            if (isConnected)
                OnSend?.Invoke(envelope);
        }

        private void Connection_OnMessage(MessageEnvelope envelope)
        {
            if (!isConnecting)
                if (!isConnected)
                    HandleHandshake(envelope);
                else
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

            public event Action<MessageEnvelope> OnSend;

            public event Action<Exception> OnException;

            public event Action<WebSocketCloseStatus> OnClosed;

            public async Task EstablishConnection()
            {
                var payload = DateTime.UtcNow.Ticks;
                var signature = Convert.ToBase64String(BitConverter.GetBytes(payload).Sign(clientKeyPair).Signature);
                webSocket.Options.SetRequestHeader("Authorization", $"ed25519 {clientKeyPair.AccountId}.{payload}.{signature}");
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

                    _ = Task.Factory.StartNew(() => OnClosed?.Invoke(status));
                }
            }

            public async Task<MessageEnvelope> SendMessage(MessageEnvelope envelope, CancellationToken ct = default)
            {
                AssignRequestId(envelope);
                AssignAccountId(envelope);
                if (!envelope.IsSignedBy(clientKeyPair))
                    envelope.Sign(clientKeyPair);

                TaskCompletionSource<MessageEnvelope> resultTask = null;
                if (envelope != hearbeatMessage
                    && envelope.Message.MessageId != default)
                {
                    resultTask = RegisterRequest(envelope);
                }

                try
                {
                    var serializedData = XdrConverter.Serialize(envelope);
                    await webSocket.SendAsync(serializedData, WebSocketMessageType.Binary, true, (ct == default ? CancellationToken.None : ct));

                    _ = Task.Factory.StartNew(() => OnSend?.Invoke(envelope));

                    if (heartbeatTimer != null)
                        heartbeatTimer.Reset();
                }
                catch (Exception exc)
                {
                    if (resultTask == null)
                        throw;
                    resultTask.SetException(exc);
                }

                return await (resultTask?.Task ?? Task.FromResult<MessageEnvelope>(null));
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

            private void AssignAccountId(MessageEnvelope envelope)
            {
                var request = envelope.Message as RequestMessage;
                if (request == null || request.Account != null)
                    return;
                request.Account = clientKeyPair;
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
                Debug.WriteLine($"{envelope.Message.MessageType} was received.");
                if (resultMessage != null)
                    lock (Requests)
                    {
                        var messageId = resultMessage.OriginalMessage.Message is RequestQuantum ?
                            ((RequestQuantum)resultMessage.OriginalMessage.Message).RequestMessage.MessageId :
                            resultMessage.OriginalMessage.Message.MessageId;
                        if (Requests.TryRemove(messageId, out var task))
                        {
                            if (resultMessage.Status == ResultStatusCodes.Success)
                                task.SetResult(envelope);
                            else
                                task.SetException(new RequestException(envelope));

                            Debug.WriteLine($"{envelope.Message.MessageType} result was set.");
                            return true;
                        }
                        else
                        {
                            Debug.WriteLine($"Unable set result for msg with id {messageId}.");
                        }
                    }
                return false;
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

        #endregion
    }
}
