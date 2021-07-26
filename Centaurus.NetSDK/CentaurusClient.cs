using System;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Centaurus.Models;
using NLog;

namespace Centaurus.NetSDK
{
    public class CentaurusClient : IDisposable
    {
        public CentaurusClient(ConstellationConfig constellationConfig)
        {
            Config = constellationConfig;
            AccountState = new AccountState();
        }

        public readonly ConstellationConfig Config;

        public AccountState AccountState { get; private set; }

        private Connection connection;

        public bool IsConnected => connection != null && connection.IsConnected;

        public event Action<Exception> OnException;

        public event Action<AccountState> OnAccountUpdate;

        public event Action OnClose;

        public async Task Connect()
        {
            try
            {
                if (connection.IsConnected) return;
                //initialize a connection
                connection = new Connection(Config);
                //subscribe to events
                connection.OnClose += Connection_OnClose;
                connection.OnException += Connection_OnException;
                //try to connect
                await connection.Connect();
                //fetch current account state
                AccountState = await GetAccountData();
                //track state on the client side
                if (AccountState != null && OnAccountUpdate != null)
                {
                    _ = Task.Factory.StartNew(() => OnAccountUpdate.Invoke(AccountState));
                }
            }
            catch (Exception e)
            {
                //failed to connect - dispose everything
                if (connection != null)
                {
                    await connection.CloseConnection(WebSocketCloseStatus.ProtocolError);
                    connection.Dispose();
                    connection = null;
                }
                throw new Exception("Failed to connect to Centaurus Alpha server", e);
            }
        }

        /// <summary>
        /// Fetch current account state from the constellation.
        /// </summary>
        /// <returns>Current account state</returns>
        public async Task<AccountState> GetAccountData()
        {
            var result = await connection.SendMessage(new AccountDataRequest().CreateEnvelope());
            var adr = result.Result.Message as AccountDataResponse;
            foreach (var balance in adr.Balances)
            {
                AccountState.balances[balance.Asset] = new BalanceModel
                {
                    AssetId = balance.Asset,
                    Amount = balance.Amount,
                    Liabilities = balance.Liabilities
                };
            }
            foreach (var order in adr.Orders)
            {
                var decodedOrderId = OrderIdConverter.Decode(order.OrderId);
                AccountState.orders[order.OrderId] = new OrderModel
                {
                    OrderId = order.OrderId,
                    Price = order.Price,
                    Amount = order.Amount,
                    AssetId = decodedOrderId.Asset,
                    Side = decodedOrderId.Side
                };
            }

            return AccountState;
        }

        private async Task<QuantumResult> Send(RequestMessage request)
        {
            if (request.MessageId == 0)
            {
                request.RequestId = DateTime.UtcNow.Ticks * (request is SequentialRequestMessage ? 1 : -1);
            }
            if (request.Account == 0)
            {
                request.Account = connection.AccountId;
            }
            var envelope = request.CreateEnvelope();
            var result = await connection.SendMessage(envelope);
            _ = result.OnFinalized.ContinueWith(task => HandleQuantumResult(task.Result));
            return result;
        }

        /// <summary>
        /// Request a payment to another account in the constellation.
        /// </summary>
        /// <param name="destination">Public key of the account that will receive funds.</param>
        /// <param name="assetId">Id of the asset to transfer</param>
        /// <param name="amount">Amount to transfer</param>
        /// <returns></returns>
        public async Task<QuantumResult> Pay(byte[] destination, int assetId, long amount)
        {
            var paymentRequest = new PaymentRequest { Amount = amount, Destination = destination, Asset = assetId };
            return await Send(paymentRequest);
        }

        /// <summary>
        /// Withdraw funds from the constellation to the supported blockchain network.
        /// </summary>
        /// <param name="network">Blockchain provider identifier</param>
        /// <param name="destination">Destination address</param>
        /// <param name="assetId">Id of the asset to withdraw</param>
        /// <param name="amount">Amount to withdraw</param>
        /// <returns></returns>
        public async Task<QuantumResult> Withdraw(string network, string destination, int assetId, long amount)
        {
            var withdrawalRequest = new WithdrawalRequest(); //TODO: set amount, asset, destination
            return await Send(withdrawalRequest);
        }

        /// <summary>
        /// Place a limit order to the constellation exchange.
        /// </summary>
        /// <param name="side">Order side - buy or sell</param>
        /// <param name="assetId">Id of the asset to trade</param>
        /// <param name="amount">Amount of the asset to buy/sell</param>
        /// <param name="price">Desired strike price</param>
        /// <returns></returns>
        public async Task<QuantumResult> PlaceOrder(OrderSide side, int assetId, long amount, double price)
        {
            //TODO: check maximum allowed order limit here
            var orderRequest = new OrderRequest { Amount = amount, Price = price, Side = side, Asset = assetId };
            return await Send(orderRequest);
        }

        /// <summary>
        /// Remove an outstanding order on the constellation exchange.
        /// </summary>
        /// <param name="orderId">Id of the order to cancel</param>
        /// <returns></returns>
        public async Task<QuantumResult> CancelOrder(ulong orderId)
        {
            var cancelOrderRequest = new OrderCancellationRequest { OrderId = orderId };
            return await Send(cancelOrderRequest);
        }

        /// <summary>
        /// Generate deposit address for a given blockchain.
        /// </summary>
        /// <param name="network">Blockchain provider identifier</param>
        /// <returns></returns>
        public DepositInstructions GetDepositAddress(string network)
        {
            //TODO: get vault address for the target network from the constellation info
            switch (network)
            {
                case "stellar":
                    return new DepositInstructions(connection.ConstellationInfo.Vault, Config.ClientKeyPair.PublicKey);
                default:
                    throw new PlatformNotSupportedException($"Blockchain network \"{network}\" is not supported.");
            }
        }

        private void HandleQuantumResult(MessageEnvelope envelope)
        {
            if (!(envelope.Message is IEffectsContainer effectsMessage)) return;
            if (effectsMessage.Effects.Count > 0)
            {
                try
                {
                    //apply effects to the client-side state
                    foreach (var effect in effectsMessage.Effects)
                    {
                        AccountState.ApplyAccountStateChanges(effect);
                    }
                    //notify subscribers about the account state update
                    OnAccountUpdate?.Invoke(AccountState);
                }
                catch (Exception e)
                {
                    OnException?.Invoke(e);
                }
            }
        }

        private void Connection_OnException(Exception exception)
        {
            OnException?.Invoke(exception);
        }

        private void Connection_OnClose()
        {
            OnClose?.Invoke();
        }

        public void Dispose()
        {
            if (connection != null)
            {
                connection.OnClose -= Connection_OnClose;
                connection.OnException -= Connection_OnException;
                connection.Dispose();
                connection = null;
            }
        }

        static CentaurusClient()
        {
            DynamicSerializersInitializer.Init();
        }

        static Logger logger = LogManager.GetCurrentClassLogger();
    }
}
