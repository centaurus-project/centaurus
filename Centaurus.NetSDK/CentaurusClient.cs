using System;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Centaurus.Models;
using NLog;
using System.Linq;
using Centaurus.Xdr;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace Centaurus.NetSDK
{
    public class CentaurusClient : IDisposable
    {
        public CentaurusClient(ConstellationConfig constellationConfig)
        {
            Config = constellationConfig ?? throw new ArgumentNullException(nameof(constellationConfig));
            AccountState = new AccountState();
        }

        public readonly ConstellationConfig Config;

        public AccountState AccountState { get; }

        internal Connection connection;

        public bool IsConnected => connection != null && connection.IsConnected;

        private ulong? lastHandledAccountSequence;

        public event Action<Exception> OnException;

        public event Action<AccountState> OnAccountUpdate;

        public event Action OnClose;

        public async Task Connect()
        {
            try
            {
                if (connection?.IsConnected ?? false)
                    return;
                //initialize a connection
                connection = new Connection(this);
                //subscribe to events
                connection.OnClose += Connection_OnClose;
                connection.OnException += Connection_OnException;
                //try to connect
                await connection.Connect();

                //fetch current account state
                await UpdateAccountData();
                //track state on the client side
                if (OnAccountUpdate != null)
                    _ = Task.Factory.StartNew(() => OnAccountUpdate.Invoke(AccountState));
            }
            catch (Exception e)
            {
                //failed to connect - dispose everything
                if (connection != null)
                    await connection.CloseConnection(WebSocketCloseStatus.ProtocolError);
                throw new Exception("Failed to connect to Centaurus Alpha server", e);
            }
        }

        public async Task Close()
        {
            if (!connection?.IsConnected ?? false)
                return;

            await connection.CloseConnection(WebSocketCloseStatus.NormalClosure);
        }

        /// <summary>
        /// Fetch current account state from the constellation.
        /// </summary>
        /// <returns>Current account state</returns>
        public async Task<AccountState> UpdateAccountData()
        {
            var result = await Send(new AccountDataRequest());
            await result.OnFinalized;

            var adr = (AccountDataResponse)result.Result.Message;

            //compute payload hash
            var payloadHash = adr.ComputePayloadHash();

            if (adr.Request is RequestHashInfo)
                throw new Exception($"Account quantum hash was received instead of full quantum data.");

            var accountQuantumRequest = XdrConverter.Deserialize<AccountDataRequestQuantum>(adr.Request.Data);

            //compare with specified one
            if (!payloadHash.SequenceEqual(accountQuantumRequest.PayloadHash))
                throw new Exception("Computed payload hash isn't equal to quantum payload hash.");

            AccountState.AccountId = connection.AccountId;
            AccountState.ConstellationInfo = connection.ConstellationInfo;

            //get last account sequence
            var effects = XdrConverter.Deserialize<EffectsGroup>(adr.Effects.First().EffectsGroupData);

            foreach (var balance in adr.Balances)
            {
                AccountState.balances[balance.Asset] = new BalanceModel
                {
                    Asset = balance.Asset,
                    Amount = balance.Amount,
                    Liabilities = balance.Liabilities
                };
            }
            foreach (var order in adr.Orders)
            {
                AccountState.orders[order.OrderId] = OrderModel.FromOrder(order);
            }

            //set sequence
            lastHandledAccountSequence = effects.AccountSequence - 1;
            //process quantum effects
            ProcessQuantumInfos();

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
            if (result != null)
            {
                _ = result.OnFinalized.ContinueWith(task =>
                {
                    if (task.IsFaulted)
                        OnException?.Invoke(task.Exception);
                });
            }
            return result;
        }

        /// <summary>
        /// Request a payment to another account in the constellation.
        /// </summary>
        /// <param name="destination">Public key of the account that will receive funds.</param>
        /// <param name="asset">Asset code to transfer</param>
        /// <param name="amount">Amount to transfer</param>
        /// <returns></returns>
        public async Task<QuantumResult> Pay(byte[] destination, string asset, ulong amount)
        {
            var paymentRequest = new PaymentRequest { Amount = amount, Destination = destination, Asset = asset };
            return await Send(paymentRequest);
        }

        /// <summary>
        /// Withdraw funds from the constellation to the supported blockchain network.
        /// </summary>
        /// <param name="provider">Blockchain provider identifier</param>
        /// <param name="destination">Destination address</param>
        /// <param name="asset">Asset code to withdraw</param>
        /// <param name="amount">Amount to withdraw</param>
        /// <returns></returns>
        public async Task<QuantumResult> Withdraw(string provider, byte[] destination, string asset, ulong amount)
        {
            var withdrawalRequest = new WithdrawalRequest
            {
                Provider = provider,
                Destination = destination,
                Asset = asset,
                Amount = amount
            };
            return await Send(withdrawalRequest);
        }

        /// <summary>
        /// Place a limit order to the constellation exchange.
        /// </summary>
        /// <param name="side">Order side - buy or sell</param>
        /// <param name="asset">Asset code to trade</param>
        /// <param name="amount">Amount of the asset to buy/sell</param>
        /// <param name="price">Desired strike price</param>
        /// <returns></returns>
        public async Task<QuantumResult> PlaceOrder(OrderSide side, string asset, ulong amount, double price)
        {
            //TODO: check maximum allowed order limit here
            var orderRequest = new OrderRequest { Amount = amount, Price = price, Side = side, Asset = asset };
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
        /// <param name="asset">Asset to deposit</param>
        /// <returns></returns>
        public DepositInstructions GetDepositAddress(string network, string asset)
        {
            var providerSettings = connection.ConstellationInfo.Providers.FirstOrDefault(p => p.Id == network);
            if (providerSettings == null)
                throw new InvalidOperationException("Provider not found.");

            var providerAsset = providerSettings.Assets.FirstOrDefault(a => a.CentaurusAsset == asset);
            if (providerAsset == null)
                throw new InvalidOperationException($"Provider doesn't support {asset} asset.");

            return new DepositInstructions(Config.ClientKeyPair.AccountId, providerSettings.Vault, providerAsset.Token);
        }

        private object quantaQueueSyncRoot = new { };
        private SortedDictionary<ulong, (ulong apex, EffectsGroup effectsGroup)> quantaQueue = new SortedDictionary<ulong, (ulong, EffectsGroup)>();

        private object accountUpdateSyncRoot = new { };

        internal void HandleQuantumNotification(IQuantumInfoContainer quantumInfo)
        {
            //find current account effects group
            var currentAccountEffects = quantumInfo.Effects.FirstOrDefault(eg => eg is EffectsInfo);
            if (currentAccountEffects == null)
            {
                return; //log it
            }

            //deserialize effects group
            var accountEffects = XdrConverter.Deserialize<EffectsGroup>(currentAccountEffects.EffectsGroupData);
            lock (quantaQueueSyncRoot)
            {
                quantaQueue.Add(accountEffects.AccountSequence, (quantumInfo.Apex, accountEffects));
                if (quantaQueue.Count > 1_000)
                {
                    throw new Exception("Effects notification length is to big.");
                }

                if (quantaQueue.Count > 20)
                    Console.WriteLine($"{Config.ClientKeyPair.AccountId}-{quantaQueue.Count}");
            }
            ProcessQuantumInfos();
        }

        private void ProcessQuantumInfos()
        {

            lock (accountUpdateSyncRoot)
            {
                //sequence wasn't initialized yet
                if (!lastHandledAccountSequence.HasValue)
                    return;
                try
                {
                    var accountEffects = default((ulong apex, EffectsGroup effectsGroup));
                    lock (quantaQueueSyncRoot)
                        accountEffects = quantaQueue.FirstOrDefault().Value;

                    while (accountEffects != default && accountEffects.effectsGroup.AccountSequence == lastHandledAccountSequence.Value + 1)
                    {
                        var apex = accountEffects.apex;
                        var effectsGroup = accountEffects.effectsGroup;
                        //apply effects to the client-side state
                        foreach (var effect in effectsGroup.Effects)
                        {
                            //assign apex and account
                            effect.Apex = apex;
                            if (effect is AccountEffect accountEffect)
                                accountEffect.Account = effectsGroup.Account;
                            AccountState.ApplyAccountStateChanges(effect);
                        }

                        //set account sequence
                        lastHandledAccountSequence = effectsGroup.AccountSequence;

                        lock (quantaQueueSyncRoot)
                        {
                            //remove current effects group from queue
                            quantaQueue.Remove(effectsGroup.AccountSequence);
                            //try get next effects group
                            accountEffects = quantaQueue.FirstOrDefault().Value;
                        }
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
    }
}
