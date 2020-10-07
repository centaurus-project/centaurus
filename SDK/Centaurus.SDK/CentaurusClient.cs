using Centaurus.Models;
using Centaurus.SDK.Models;
using Centaurus.Xdr;
using NLog;
using NSec.Cryptography;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.xdr;
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
    public class CentaurusClient : IDisposable
    {

        static Logger logger = LogManager.GetCurrentClassLogger();

        const int timeout = 5000;
        public CentaurusClient(Uri alphaWebSocketAddress, KeyPair keyPair, ConstellationInfo constellationInfo)
        {
            this.alphaWebSocketAddress = alphaWebSocketAddress ?? throw new ArgumentNullException(nameof(alphaWebSocketAddress));
            this.keyPair = keyPair ?? throw new ArgumentNullException(nameof(keyPair));
            this.constellation = constellationInfo ?? throw new ArgumentNullException(nameof(constellationInfo));
        }

        public async Task Connect()
        {
            try
            {
                await connectionSemaphore.WaitAsync();

                connection = new CentaurusConnection(alphaWebSocketAddress, keyPair, constellation);
                SubscribeToEvents(connection);
                await connection.EstablishConnection();

                await handshakeResult.Task;
            }
            catch
            {
                UnsubscribeFromEvents(connection);
                if (connection != null)
                    await connection.CloseConnection();
                connection = null;
                throw new Exception("Login failed.");
            }
            finally
            {

                connectionSemaphore.Release();
            }
        }

        public async Task CloseConnection()
        {
            try
            {
                connectionSemaphore.Wait();
                if (connection != null)
                    await connection.CloseAndDispose();
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
                Balances = rawMessage.Balances.Select(x => BalanceModel.FromBalance(x)).ToList(),
                Orders = rawMessage.Orders.Select(x => OrderModel.FromOrder(x)).ToList()
            };
        }

        public async Task<MessageEnvelope> Withdrawal(KeyPair destination, string amount, ConstellationInfo.Asset asset)
        {
            var paymentMessage = new WithdrawalRequest();
            var tx = await TransactionHelper.GetPaymentTx(keyPair, constellation, destination, amount, asset);

            paymentMessage.TransactionXdr = tx.ToArray();

            var result = await connection.SendMessage(paymentMessage.CreateEnvelope());
            var txResultMessage = result.Message as ITransactionResultMessage;
            if (txResultMessage is null)
                throw new Exception($"Unexpected result type '{result.Message.MessageType}'");

            Network.Use(new Network(constellation.StellarNetwork.Passphrase));
            tx.Sign(keyPair);
            foreach (var signature in txResultMessage.TxSignatures)
            {
                tx.Signatures.Add(signature.ToDecoratedSignature());
            }

            var submitResult = await tx.Submit(constellation);
            if (!submitResult.IsSuccess())
            {
                logger.Error($"Submit withdrawal failed. Result xdr: {submitResult.ResultXdr}");
            }
            return result;
        }

        public async Task<MessageEnvelope> CreateOrder(long amount, double price, OrderSides side, ConstellationInfo.Asset asset)
        {
            var order = new OrderRequest { Amount = amount, Price = price, Side = side, Asset = asset.Id };
            var result = await connection.SendMessage(order.CreateEnvelope());
            return result;
        }

        public async Task<MessageEnvelope> CancelOrder(ulong orderId)
        {
            var order = new OrderCancellationRequest { OrderId = orderId };
            var result = await connection.SendMessage(order.CreateEnvelope());
            return result;
        }

        public async Task<MessageEnvelope> MakePayment(KeyPair destination, long amount, ConstellationInfo.Asset asset)
        {
            var paymentMessage = new PaymentRequest { Amount = amount, Destination = destination, Asset = asset.Id };
            var result = await connection.SendMessage(paymentMessage.CreateEnvelope());
            return result;
        }

        public void Dispose()
        {
            UnsubscribeFromEvents(connection);
            CloseConnection().Wait();
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
        private ConstellationInfo constellation;
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
                logger.Trace("Handshake: started.");
                isConnecting = true;
                logger.Trace("Handshake: isConnecting is set to true.");
                try
                {
                    var resultTask = connection.SendMessage(envelope.Message.CreateEnvelope());
                    logger.Trace("Handshake: message is sent.");
                    var result = (ResultMessage)resultTask.Result.Message;
                    logger.Trace("Handshake: response awaited.");
                    if (result.Status != ResultStatusCodes.Success)
                        throw new Exception();
                    handshakeResult.SetResult(true);
                    logger.Trace("Handshake: result is set to true.");
                    isConnected = true;
                    logger.Trace("Handshake: isConnected is set.");
                }
                catch (Exception exc)
                {
                    handshakeResult.TrySetException(exc);
                    logger.Trace("Handshake: exception is set.");
                }
                isConnecting = false;
                logger.Trace("Handshake: isConnecting is set to false.");
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
            logger.Trace($"OnMessage: {envelope.Message.MessageType}");
            if (!isConnecting)
            {
                if (!isConnected)
                {
                    logger.Trace($"OnMessage: no connected.");
                    HandleHandshake(envelope);
                }
                else
                {
                    logger.Trace($"OnMessage: connected.");
                    OnMessage?.Invoke(envelope);
                }
            }
            else
            {
                logger.Trace($"OnMessage: already connecting");
            }
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
            public CentaurusConnection(Uri _websocketAddress, KeyPair _clientKeyPair, ConstellationInfo _constellationInfo)
            {
                clientKeyPair = _clientKeyPair ?? throw new ArgumentNullException(nameof(_clientKeyPair));
                websocketAddress = new Uri(_websocketAddress ?? throw new ArgumentNullException(nameof(_websocketAddress)), "centaurus");
                constellationInfo = _constellationInfo ?? throw new ArgumentNullException(nameof(_constellationInfo));

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
                //var payload = DateTime.UtcNow.Ticks;
                //var signature = Convert.ToBase64String(BitConverter.GetBytes(payload).Sign(clientKeyPair).Signature);
                //webSocket.Options.SetRequestHeader("Authorization", $"ed25519 {clientKeyPair.AccountId}.{payload}.{signature}");
                await webSocket.ConnectAsync(websocketAddress, CancellationToken.None);
                _ = Listen();
            }

            public async Task CloseConnection(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string desc = null)
            {
                if (webSocket == null)
                    return;
                try
                {
                    if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting)
                        await webSocket.CloseAsync(status, desc, CancellationToken.None);
                }
                catch (WebSocketException exc)
                {
                    //ignore "closed-without-completing-handshake" error
                    if (exc.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely)
                        throw;
                }
                var allPendingRequests = Requests.Values;
                var closeException = new ConnectionCloseException(status, desc);
                foreach (var pendingRequest in allPendingRequests)
                    pendingRequest.SetException(closeException);

                if (status != WebSocketCloseStatus.NormalClosure)
                    _ = Task.Factory.StartNew(() => OnException?.Invoke(closeException));

                _ = Task.Factory.StartNew(() => OnClosed?.Invoke(status));
            }

            public async Task<MessageEnvelope> SendMessage(MessageEnvelope envelope, CancellationToken ct = default)
            {
                AssignRequestId(envelope);
                AssignAccountId(envelope);
                if (!envelope.IsSignedBy(clientKeyPair))
                    envelope.Sign(clientKeyPair);

                CentaurusResponse resultTask = null;
                if (envelope != hearbeatMessage
                    && envelope.Message.MessageId != default)
                {
                    resultTask = RegisterRequest(envelope);
                }

                try
                {
                    var serializedData = XdrConverter.Serialize(envelope);
                    Debug.WriteLine($"Message before sent: {envelope.Message.GetType().Name}_{envelope.Message.MessageId}");
                    await webSocket.SendAsync(serializedData, WebSocketMessageType.Binary, true, (ct == default ? CancellationToken.None : ct));
                    Debug.WriteLine($"Message after sent: {envelope.Message.GetType().Name}_{envelope.Message.MessageId}");

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

                return await (resultTask?.ResponseTask ?? Task.FromResult<MessageEnvelope>(null));
            }

            public async Task CloseAndDispose(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string desc = null)
            {
                await CloseConnection(status);
                Dispose();
            }

            public void Dispose()
            {
                webSocket?.Dispose();
                webSocket = null;
            }

            #region Private Members

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
            private ConstellationInfo constellationInfo;
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

            private CentaurusResponse RegisterRequest(MessageEnvelope envelope)
            {
                lock (Requests)
                {
                    var messageId = envelope.Message.MessageId;
                    var response = envelope.Message is NonceRequestMessage ? new CentaurusQuantumResponse(constellationInfo, timeout) : new CentaurusResponse(constellationInfo, timeout);
                    if (!Requests.TryAdd(messageId, response))
                        throw new Exception("Unable to add request to pending requests.");
                    return response;
                }
            }

            private bool TryResolveRequest(MessageEnvelope envelope)
            {
                var resultMessage = envelope.Message as ResultMessage;

                if (resultMessage != null)
                {
                    lock (Requests)
                    {
                        var messageId = resultMessage.OriginalMessage.Message is RequestQuantum ?
                            ((RequestQuantum)resultMessage.OriginalMessage.Message).RequestMessage.MessageId :
                            resultMessage.OriginalMessage.Message.MessageId;
                        if (Requests.TryGetValue(messageId, out var task))
                        {
                            task.AssignResponse(envelope);
                            if (task.IsCompleted)
                                Requests.TryRemove(messageId, out _);
                            logger.Trace($"{envelope.Message.MessageType}:{messageId} result was set.");
                            return true;
                        }
                        else
                        {
                            logger.Trace($"Unable set result for msg with id {envelope.Message.MessageType}:{messageId}.");
                        }
                    }
                }
                else
                {
                    logger.Trace("Request is not result message.");
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
                    await CloseConnection(WebSocketCloseStatus.ProtocolError, e.Message);
                }
                catch (ConnectionCloseException e)
                {
                    await CloseConnection(e.Status, e.Description);
                }
                catch (Exception e)
                {
                    await CloseConnection(WebSocketCloseStatus.InternalServerError, e.Message);
                }
            }

            private ConcurrentDictionary<long, CentaurusResponse> Requests { get; } = new ConcurrentDictionary<long, CentaurusResponse>();

            private void HandleMessage(MessageEnvelope envelope)
            {
                TryResolveRequest(envelope);
                Thread.Sleep(100);
                _ = Task.Factory.StartNew(() => OnMessage?.Invoke(envelope));
            }

            #endregion
        }

        #endregion
    }
}
