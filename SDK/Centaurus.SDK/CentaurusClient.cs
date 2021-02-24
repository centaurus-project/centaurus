using Centaurus.Models;
using Centaurus.SDK.Models;
using Centaurus.Xdr;
using NLog;
using NSec.Cryptography;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        const int timeout = 15000;
        public CentaurusClient(Uri alphaWebSocketAddress, KeyPair keyPair, ConstellationInfo constellationInfo)
        {
            this.alphaWebSocketAddress = alphaWebSocketAddress ?? throw new ArgumentNullException(nameof(alphaWebSocketAddress));
            this.keyPair = keyPair ?? throw new ArgumentNullException(nameof(keyPair));
            this.constellation = constellationInfo ?? throw new ArgumentNullException(nameof(constellationInfo));

            Network.Use(new Network(constellation.StellarNetwork.Passphrase));
        }

        public async Task Connect()
        {
            try
            {
                await connectionSemaphore.WaitAsync();

                if (IsConnected)
                    return;

                handshakeResult = new TaskCompletionSource<int>();

                connection = new CentaurusConnection(alphaWebSocketAddress, keyPair, constellation);
                SubscribeToEvents(connection);
                await connection.EstablishConnection();

                await handshakeResult.Task;

                await UpdateAccountData();

                IsConnected = true;
            }
            catch (Exception exc)
            {
                UnsubscribeFromEvents(connection);
                if (connection != null)
                    await connection.CloseAndDispose();
                connection = null;
                throw new Exception("Login failed.", exc);
            }
            finally
            {
                connectionSemaphore.Release();
            }
        }

        public async Task UpdateAccountData()
        {
            AccountData = await GetAccountData();
        }

        public async Task CloseConnection()
        {
            try
            {
                connectionSemaphore.Wait();
                UnsubscribeFromEvents(connection);
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

        public event Action<AccountDataModel> OnAccountUpdate;

        public event Action OnClosed;

        public async Task<AccountDataModel> GetAccountData(bool waitForFinalize = true)
        {
            var response = (CentaurusResponse)await connection.SendMessage(new AccountDataRequest().CreateEnvelope());
            var data = await (waitForFinalize ? response.ResponseTask : response.AcknowledgmentTask);
            var rawMessage = (AccountDataResponse)data.Message;
            var balances = rawMessage.Balances.Select(x => BalanceModel.FromBalance(x, constellation)).ToDictionary(k => k.AssetId, v => v);
            var orders = rawMessage.Orders.Select(x => OrderModel.FromOrder(x, constellation)).ToDictionary(k => k.OrderId, v => v);
            return new AccountDataModel
            {
                Balances = balances,
                Orders = orders
            };
        }

        public async Task<MessageEnvelope> Withdrawal(KeyPair destination, string amount, ConstellationInfo.Asset asset, bool waitForFinalize = true)
        {
            var paymentMessage = new WithdrawalRequest();
            var tx = await TransactionHelper.GetWithdrawalTx(keyPair, constellation, destination, amount, asset);

            paymentMessage.TransactionXdr = tx.ToArray();

            var response = (CentaurusResponse)await connection.SendMessage(paymentMessage.CreateEnvelope());
            var result = await (waitForFinalize ? response.ResponseTask : response.AcknowledgmentTask);
            var txResultMessage = result.Message as ITransactionResultMessage;
            if (txResultMessage is null)
                throw new Exception($"Unexpected result type '{result.Message.MessageType}'");

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

        public async Task<MessageEnvelope> CreateOrder(long amount, double price, OrderSide side, ConstellationInfo.Asset asset, bool waitForFinalize = true)
        {
            var order = new OrderRequest { Amount = amount, Price = price, Side = side, Asset = asset.Id };

            var response = (CentaurusResponse)await connection.SendMessage(order.CreateEnvelope());
            var result = await (waitForFinalize ? response.ResponseTask : response.AcknowledgmentTask);
            return result;
        }

        public async Task<MessageEnvelope> CancelOrder(ulong orderId, bool waitForFinalize = true)
        {
            var order = new OrderCancellationRequest { OrderId = orderId };
            var response = (CentaurusResponse)await connection.SendMessage(order.CreateEnvelope());
            var result = await (waitForFinalize ? response.ResponseTask : response.AcknowledgmentTask);
            return result;
        }

        public async Task<MessageEnvelope> MakePayment(KeyPair destination, long amount, ConstellationInfo.Asset asset, bool waitForFinalize = true)
        {
            var paymentMessage = new PaymentRequest { Amount = amount, Destination = destination, Asset = asset.Id };
            var response = (CentaurusResponse)await connection.SendMessage(paymentMessage.CreateEnvelope());
            var result = await (waitForFinalize ? response.ResponseTask : response.AcknowledgmentTask);
            return result;
        }

        public async Task Register(long amount, params KeyPair[] extraSigners)
        {
            //try to connect to make sure that pubkey is not registered yet
            try { await Connect(); } catch { }

            if (IsConnected)
                throw new Exception("Already registered.");

            if (amount < constellation.MinAccountBalance)
                throw new Exception($"Min allowed account balance is {amount}.");

            await Deposit(amount, constellation.Assets.First(a => a.Issuer == null), extraSigners);
            var tries = 0;
            while (true)
                try
                {
                    await Connect();
                    break;
                }
                catch
                {
                    if (++tries < 10)
                    {
                        await Task.Delay(3000);
                        continue;
                    }
                    throw new Exception("Unable to login. Maybe server is too busy. Try later.");
                }
        }

        public async Task Deposit(long amount, ConstellationInfo.Asset asset, params KeyPair[] extraSigners)
        {
            var tx = await TransactionHelper.GetDepositTx(keyPair, constellation, Amount.FromXdr(amount), asset);
            tx.Sign(keyPair);
            foreach (var signer in extraSigners)
                tx.Sign(signer);
            await tx.Submit(constellation);
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
        private TaskCompletionSource<int> handshakeResult;

        private List<long> processedEffectsMessages = new List<long>();

        private void RegisterNewEffectsMessage(long messageId)
        {
            processedEffectsMessages.Add(messageId);
            if (processedEffectsMessages.Count > 100_000)
                processedEffectsMessages.RemoveAt(0);
        }

        private void ResultMessageHandler(MessageEnvelope envelope)
        {
            lock (processedEffectsMessages)
            {
                if (AccountData == null
                   || !(envelope.Message is IEffectsContainer effectsMessage)
                   || processedEffectsMessages.Any(r => r == envelope.Message.MessageId))
                    return;
                RegisterNewEffectsMessage(envelope.Message.MessageId);
                try
                {
                    foreach (var effect in effectsMessage.Effects)
                    {
                        switch (effect)
                        {
                            case NonceUpdateEffect nonceUpdateEffect:
                                AccountData.Nonce = nonceUpdateEffect.Nonce;
                                break;
                            case BalanceCreateEffect balanceCreateEffect:
                                AccountData.AddBalance(balanceCreateEffect.Asset, constellation);
                                break;
                            case BalanceUpdateEffect balanceUpdateEffect:
                                AccountData.UpdateBalance(balanceUpdateEffect.Asset, balanceUpdateEffect.Amount);
                                break;
                            case OrderPlacedEffect orderPlacedEffect:
                                {
                                    AccountData.AddOrder(orderPlacedEffect.OrderId, orderPlacedEffect.Amount, orderPlacedEffect.Price, constellation);
                                    var decodedId = OrderIdConverter.Decode(orderPlacedEffect.OrderId);
                                    if (decodedId.Side == OrderSide.Buy)
                                        AccountData.UpdateLiabilities(0, orderPlacedEffect.QuoteAmount);
                                    else
                                        AccountData.UpdateLiabilities(decodedId.Asset, orderPlacedEffect.Amount);
                                }
                                break;
                            case OrderRemovedEffect orderRemoveEffect:
                                {
                                    AccountData.RemoveOrder(orderRemoveEffect.OrderId);
                                    var decodedId = OrderIdConverter.Decode(orderRemoveEffect.OrderId);
                                    if (decodedId.Side == OrderSide.Buy)
                                        AccountData.UpdateLiabilities(0, -orderRemoveEffect.QuoteAmount);
                                    else
                                        AccountData.UpdateLiabilities(decodedId.Asset, -orderRemoveEffect.Amount);
                                }
                                break;
                            case TradeEffect tradeEffect:
                                {
                                    AccountData.UpdateOrder(tradeEffect.OrderId, tradeEffect.AssetAmount);

                                    var decodedId = OrderIdConverter.Decode(tradeEffect.OrderId);
                                    if (decodedId.Side == OrderSide.Buy)
                                    {
                                        if (!tradeEffect.IsNewOrder)
                                            AccountData.UpdateLiabilities(0, -tradeEffect.QuoteAmount);
                                        AccountData.UpdateBalance(0, -tradeEffect.QuoteAmount);
                                        AccountData.UpdateBalance(decodedId.Asset, tradeEffect.AssetAmount);
                                    }
                                    else
                                    {
                                        if (!tradeEffect.IsNewOrder)
                                            AccountData.UpdateLiabilities(decodedId.Asset, -tradeEffect.AssetAmount);
                                        AccountData.UpdateBalance(decodedId.Asset, -tradeEffect.AssetAmount);
                                        AccountData.UpdateBalance(0, tradeEffect.QuoteAmount);
                                    }
                                }
                                break;
                            case WithdrawalCreateEffect withdrawalCreateEffect:
                                foreach (var withdrawalItem in withdrawalCreateEffect.Items)
                                {
                                    AccountData.UpdateLiabilities(withdrawalItem.Asset, withdrawalItem.Amount);
                                }
                                break;
                            case WithdrawalRemoveEffect withdrawalRemoveEffect:
                                foreach (var withdrawalItem in withdrawalRemoveEffect.Items)
                                {
                                    if (withdrawalRemoveEffect.IsSuccessful)
                                        AccountData.UpdateBalance(withdrawalItem.Asset, -withdrawalItem.Amount);
                                    AccountData.UpdateLiabilities(withdrawalItem.Asset, -withdrawalItem.Amount);
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    OnAccountUpdate?.Invoke(AccountData);
                }
                catch (Exception exc)
                {
                    OnException?.Invoke(exc);
                }
            }
        }

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
                    var responseTask = (CentaurusResponse)connection.SendMessage(envelope.Message.CreateEnvelope()).Result;
                    logger.Trace("Handshake: message is sent.");
                    var result = (HandshakeResult)responseTask.ResponseTask.Result.Message;
                    logger.Trace("Handshake: response awaited.");
                    if (result.Status != ResultStatusCodes.Success)
                        throw new Exception();
                    connection.AssignAccountId(result.AccountId);
                    handshakeResult.SetResult(result.AccountId);
                    logger.Trace("Handshake: result is set to true.");
                    IsConnected = true;
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

        public bool IsConnected { get; private set; }

        private bool isConnecting = false;

        private AccountDataModel accountData;
        public AccountDataModel AccountData
        {
            get => accountData;
            private set
            {
                accountData = value;
                OnAccountUpdate?.Invoke(accountData);
            }
        }

        private void Connection_OnSend(MessageEnvelope envelope)
        {
            if (IsConnected)
                OnSend?.Invoke(envelope);
        }

        private void Connection_OnMessage(MessageEnvelope envelope)
        {
            logger.Trace($"OnMessage: {envelope.Message.MessageType}");
            if (!isConnecting)
            {
                if (!IsConnected)
                {
                    logger.Trace($"OnMessage: no connected.");
                    HandleHandshake(envelope);
                }
                else
                {
                    logger.Trace($"OnMessage: connected.");
                    ResultMessageHandler(envelope);
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

        private void Connection_OnClosed()
        {
            IsConnected = false;
            OnClosed?.Invoke();
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

                cancellationTokenSource = new CancellationTokenSource();
                cancellationToken = cancellationTokenSource.Token;

                InitTimer();
            }

            private CancellationTokenSource cancellationTokenSource;
            private CancellationToken cancellationToken;

            public event Action<MessageEnvelope> OnMessage;

            public event Action<MessageEnvelope> OnSend;

            public event Action<Exception> OnException;

            public event Action OnClosed;

            public async Task EstablishConnection()
            {
                await webSocket.ConnectAsync(websocketAddress, cancellationToken);
                _ = Listen();
            }

            public async Task CloseConnection(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string desc = null)
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        await sendMessageSemaphore.WaitAsync();
                        try
                        {
                            var timeoutTokenSource = new CancellationTokenSource(1000);
                            await webSocket.CloseAsync(status, desc, timeoutTokenSource.Token);
                            cancellationTokenSource.Cancel();
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
                            sendMessageSemaphore.Release();
                        }
                    }
                }
                catch (WebSocketException exc)
                {
                    //ignore "closed-without-completing-handshake" error and invalid state error
                    if (exc.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely
                        && exc.WebSocketErrorCode != WebSocketError.InvalidState)
                        throw;
                }
                var allPendingRequests = Requests.Values;
                var closeException = new ConnectionCloseException(status, desc);
                foreach (var pendingRequest in allPendingRequests)
                    pendingRequest.SetException(closeException);

                if (status != WebSocketCloseStatus.NormalClosure)
                    _ = Task.Factory.StartNew(() => OnException?.Invoke(closeException));
            }

            public void AssignAccountId(int accountId)
            {
                this.accountId = accountId;
            }

            private SemaphoreSlim sendMessageSemaphore = new SemaphoreSlim(1);

            public async Task<CentaurusResponseBase> SendMessage(MessageEnvelope envelope)
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


                await sendMessageSemaphore.WaitAsync();
                try
                {
                    var serializedData = XdrConverter.Serialize(envelope);

                    using var buffer = XdrBufferFactory.Rent();
                    using var writer = new XdrBufferWriter(buffer.Buffer);
                    XdrConverter.Serialize(envelope, writer);

                    if (webSocket.State == WebSocketState.Open)
                        await webSocket.SendAsync(buffer.AsSegment(0, writer.Length), WebSocketMessageType.Binary, true, cancellationToken);

                    _ = Task.Factory.StartNew(() => OnSend?.Invoke(envelope));

                    heartbeatTimer?.Reset();
                }
                catch (WebSocketException e)
                {
                    if (resultTask != null)
                        resultTask.SetException(e);

                    await CloseConnection(WebSocketCloseStatus.ProtocolError, e.Message);
                }
                catch (Exception exc)
                {
                    if (resultTask == null)
                        throw;
                    resultTask.SetException(exc);
                }
                finally
                {
                    sendMessageSemaphore.Release();
                }

                return resultTask ?? (CentaurusResponseBase)new VoidResponse();
            }

            public async Task CloseAndDispose(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string desc = null)
            {
                await CloseConnection(status, desc);
                Dispose();
            }

            public void Dispose()
            {
                if (heartbeatTimer != null)
                {
                    heartbeatTimer.Elapsed -= HeartbeatTimer_Elapsed;
                    heartbeatTimer.Dispose();
                    heartbeatTimer = null;
                }

                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;

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
            private int accountId;
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
                request.RequestId = request is SequentialRequestMessage ? currentTicks : -currentTicks;
            }

            private void AssignAccountId(MessageEnvelope envelope)
            {
                var request = envelope.Message as RequestMessage;
                if (request == null || request.Account > 0)
                    return;
                request.Account = accountId;
            }

            private CentaurusResponse RegisterRequest(MessageEnvelope envelope)
            {
                lock (Requests)
                {
                    var messageId = envelope.Message.MessageId;
                    var response = envelope.Message is SequentialRequestMessage ? new CentaurusQuantumResponse(constellationInfo.VaultPubKey, constellationInfo.AuditorPubKeys, timeout) : new CentaurusResponse(constellationInfo.VaultPubKey, constellationInfo.AuditorPubKeys, timeout);
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

            private MessageEnvelope DeserializeMessage(XdrBufferFactory.RentedBuffer buffer)
            {
                try
                {
                    var reader = new XdrBufferReader(buffer.Buffer, buffer.Length);
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
                    while (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted && !cancellationToken.IsCancellationRequested)
                    {
                        var result = await webSocket.GetWebsocketBuffer(WebSocketExtension.ChunkSize * 100, cancellationToken);
                        using (result.messageBuffer)
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                //the client send close message
                                if (result.messageType == WebSocketMessageType.Close)
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
                                    try
                                    {
                                        HandleMessage(DeserializeMessage(result.messageBuffer));
                                    }
                                    catch (UnexpectedMessageException e)
                                    {
                                        _ = Task.Factory.StartNew(() => OnException?.Invoke(e));
                                    }
                                }
                            }
                    }
                }
                catch (OperationCanceledException)
                { }
                catch (Exception e)
                {
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
                    //make sure the socket is closed
                    if (webSocket.State != WebSocketState.Closed)
                        webSocket.Abort();
                    _ = Task.Factory.StartNew(() => OnClosed?.Invoke());
                }
            }

            private ConcurrentDictionary<long, CentaurusResponse> Requests = new ConcurrentDictionary<long, CentaurusResponse>();

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
