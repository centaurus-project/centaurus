using Centaurus.DAL;
using Centaurus.DAL.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Centaurus.Models;
using Centaurus.Xdr;
using System.Diagnostics;
using Centaurus.DAL.Mongo;

namespace Centaurus.Domain
{
    //TODO: rename and separate it.
    public class PersistenceManager
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private SemaphoreSlim saveSnapshotSemaphore = new SemaphoreSlim(1);
        private IStorage storage;

        public PersistenceManager(IStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public async Task<EffectsResponse> LoadEffects(string rawCursor, bool isDesc, int limit, int account)
        {
            if (account == default)
                throw new ArgumentNullException(nameof(account));
            if (string.IsNullOrEmpty(rawCursor))
                rawCursor = "0";
            if (!long.TryParse(rawCursor, out var apex))
                throw new ArgumentException("Cursor is invalid.");
            var accountQuanta = (await storage.LoadEffects(apex, isDesc, limit, account));
            var effectModels = accountQuanta
                .OrderBy(e => e.Apex)
                .Select(ae => new ApexEffects
                {
                    Apex = ae.Apex,
                    Items = ae.ToQuantumContainer().Effects.Where(a => a.Account == account).ToList()
                })
                .ToList();
            if (isDesc)
            {
                effectModels.Reverse();
                effectModels.ForEach(ae => ae.Items.Reverse());
            }
            return new EffectsResponse
            {
                CurrentPagingToken = rawCursor,
                Order = isDesc ? EffectsRequest.Desc : EffectsRequest.Asc,
                Items = effectModels,
                NextPageToken = effectModels.LastOrDefault()?.Apex.ToString(),
                PrevPageToken = effectModels.FirstOrDefault()?.Apex.ToString(),
                Limit = limit
            };
        }

        public async Task<int> ApplyUpdates(DiffObject updates)
        {
            await saveSnapshotSemaphore.WaitAsync();
            try
            {
                return await storage.Update(updates);
            }
            finally
            {
                saveSnapshotSemaphore.Release();
            }
        }

        /// <summary>
        /// Fetches all quanta where apex is greater than the specified one.
        /// </summary>
        /// <param name="apex"></param>
        /// <param name="count">Count of quanta to load. Loads all if equal or less than 0</param>
        /// <returns></returns>
        public async Task<List<MessageEnvelope>> GetQuantaAboveApex(long apex, int count = 0)
        {
            var quantaModels = await storage.LoadQuantaAboveApex(apex, count);
            return quantaModels.OrderBy(q => q.Apex).Select(q => XdrConverter.Deserialize<MessageEnvelope>(q.Bin)).ToList();
        }

        public async Task<long> GetLastApex()
        {
            return await storage.GetLastApex();
        }

        public async Task<MessageEnvelope> GetQuantum(long apex)
        {
            var quantumModel = await storage.LoadQuantum(apex);
            return XdrConverter.Deserialize<MessageEnvelope>(quantumModel.Bin);
        }

        /// <summary>
        /// Fetches current snapshot
        /// </summary>
        /// <returns></returns>
        public async Task<Snapshot> GetSnapshot()
        {
            var lastApex = await GetLastApex();
            if (lastApex < 0)
                return null;
            return await GetSnapshot(lastApex);
        }

        /// <summary>
        /// Creates snapshot from effect
        /// </summary>
        /// <returns></returns>
        public static Snapshot GetSnapshot(ConstellationInitEffect constellationInitEffect, byte[] quantumHash)
        {
            var assets = new List<AssetSettings> { new AssetSettings() };
            assets.AddRange(constellationInitEffect.Assets);

            var snapshot = new Snapshot
            {
                Apex = constellationInitEffect.Apex,
                Accounts = new List<AccountWrapper>(),
                Orders = new List<Order>(),
                Withdrawals = new Dictionary<PaymentProvider, WithdrawalStorage>(),
                Settings = new ConstellationSettings
                {
                    Apex = constellationInitEffect.Apex,
                    Assets = assets,
                    Auditors = constellationInitEffect.Auditors,
                    MinAccountBalance = constellationInitEffect.MinAccountBalance,
                    MinAllowedLotSize = constellationInitEffect.MinAllowedLotSize,
                    Vaults = constellationInitEffect.Vaults,
                    RequestRateLimits = constellationInitEffect.RequestRateLimits
                },
                Cursors = constellationInitEffect.Cursors,
                LastHash = quantumHash
            };
            return snapshot;
        }

        /// <summary>
        /// Fetches settings for the specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public async Task<ConstellationSettings> GetConstellationSettings(long apex)
        {
            var settingsModel = await storage.LoadSettings(apex);
            if (settingsModel == null)
                return null;

            return settingsModel.ToSettings();
        }

        /// <summary>
        /// Builds snapshot for specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public async Task<Snapshot> GetSnapshot(long apex)
        {
            if (apex < 0)
                throw new ArgumentException("Apex cannot be less than zero.");

            var lastApex = await GetLastApex();
            if (lastApex < apex)
                throw new InvalidOperationException("Requested apex is greater than the last known one.");

            //some auditors can have capped db
            var minRevertApex = await GetMinRevertApex();
            if (minRevertApex == -1 && apex != lastApex || apex < minRevertApex)
                throw new InvalidOperationException($"Lack of data to revert to {apex} apex.");

            var settings = await GetConstellationSettings(apex);
            if (settings == null)
                return null;

            var cursors = await storage.LoadCursors();

            var accounts = (await GetAccounts()).Select(a => new AccountWrapper(a, settings.RequestRateLimits));

            var accountStorage = new AccountStorage(accounts);

            var withdrawals = await GetWithdrawals(accountStorage, settings);

            var orders = await GetOrders(accountStorage);

            var exchange = await GetRestoredExchange(orders);

            var batchSize = 1000;
            var effects = new List<Effect>();
            while (true)
            {
                var quanta = await storage.LoadQuantaAboveApex(apex, batchSize);
                effects.AddRange(quanta.SelectMany(q => q.ToQuantumContainer(accountStorage).Effects));
                if (quanta.Count < batchSize)
                    break;
            }

            for (var i = effects.Count - 1; i >= 0; i--)
            {
                var currentEffect = effects[i];
                var account = currentEffect.AccountWrapper;
                IEffectProcessor<Effect> processor = null;
                switch (currentEffect)
                {
                    case AccountCreateEffect accountCreateEffect:
                        processor = new AccountCreateEffectProcessor(accountCreateEffect, accountStorage, settings.RequestRateLimits);
                        break;
                    case NonceUpdateEffect nonceUpdateEffect:
                        processor = new NonceUpdateEffectProcessor(nonceUpdateEffect);
                        break;
                    case BalanceCreateEffect balanceCreateEffect:
                        processor = new BalanceCreateEffectProcessor(balanceCreateEffect);
                        break;
                    case BalanceUpdateEffect balanceUpdateEffect:
                        processor = new BalanceUpdateEffectProcesor(balanceUpdateEffect);
                        break;
                    case RequestRateLimitUpdateEffect requestRateLimitUpdateEffect:
                        processor = new RequestRateLimitUpdateEffectProcessor(requestRateLimitUpdateEffect, settings.RequestRateLimits);
                        break;
                    case OrderPlacedEffect orderPlacedEffect:
                        {
                            var orderBook = exchange.GetOrderbook(orderPlacedEffect.OrderId);
                            var order = exchange.OrderMap.GetOrder(orderPlacedEffect.OrderId);
                            processor = new OrderPlacedEffectProcessor(orderPlacedEffect, orderBook, order);
                        }
                        break;
                    case OrderRemovedEffect orderRemovedEffect:
                        {
                            var orderBook = exchange.GetOrderbook(orderRemovedEffect.OrderId);
                            processor = new OrderRemovedEffectProccessor(orderRemovedEffect, orderBook);
                        }
                        break;
                    case TradeEffect tradeEffect:
                        {
                            var order = exchange.OrderMap.GetOrder(tradeEffect.OrderId);
                            if (order == null) //no need to revert trade if no order was created
                                continue;
                            processor = new TradeEffectProcessor(tradeEffect, order);
                        }
                        break;
                    case WithdrawalCreateEffect withdrawalCreate:
                        {
                            if (!withdrawals.TryGetValue(withdrawalCreate.Provider, out var storage))
                                throw new Exception($"No storage for provider {withdrawalCreate.Provider}.");

                            var withdrawal = storage.GetWithdrawal(withdrawalCreate.Apex);
                            processor = new WithdrawalCreateEffectProcessor(withdrawalCreate, withdrawal, storage);
                        }
                        break;
                    case WithdrawalRemoveEffect withdrawalRemove:
                        {
                            if (!withdrawals.TryGetValue(withdrawalRemove.Provider, out var storage))
                            {
                                storage = new WithdrawalStorage();
                                withdrawals.Add(withdrawalRemove.Provider, storage);
                            }
                            var withdrawal = storage.GetWithdrawal(withdrawalRemove.Apex);
                            processor = new WithdrawalRemoveEffectProcessor(withdrawalRemove, withdrawal, storage);
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }

                processor.RevertEffect();
            }

            var lastQuantumData = (await storage.LoadQuantum(apex)).ToQuantumContainer();

            //TODO: refactor restore exchange
            //we need to clean all order links to be able to restore exchange
            var allOrders = exchange.OrderMap.GetAllOrders();
            foreach (var order in orders)
            {
                order.Next = null;
                order.Prev = null;
            }

            return new Snapshot
            {
                Apex = apex,
                Accounts = accountStorage.GetAll().OrderBy(a => a.Account.Id).ToList(),
                Cursors = cursors.Select(c => new PaymentCursor { Cursor = c.Cursor, Provider = (PaymentProvider)c.Provider }).ToList(),
                Orders = allOrders.OrderBy(o => o.OrderId).ToList(),
                Settings = settings,
                Withdrawals = withdrawals,
                LastHash = lastQuantumData.Quantum.Message.ComputeHash()
            };
        }

        /// <summary>
        /// Returns minimal apex a snapshot can be reverted to
        /// </summary>
        /// <returns></returns>
        public async Task<long> GetMinRevertApex()
        {
            //obtain min apex we can revert to
            var minApex = await storage.GetFirstApex();
            if (minApex == -1) //we can't revert at all
                return -1;

            return minApex - 1; //we can revert effect for that apex, so the minimal apex is first effect apex - 1
        }

        private async Task<Exchange> GetRestoredExchange(List<Order> orders)
        {
            var settings = await GetConstellationSettings(long.MaxValue); // load last settings
            return Exchange.RestoreExchange(settings.Assets, orders, false);
        }

        private async Task<List<Account>> GetAccounts()
        {
            var accountModels = await storage.LoadAccounts();
            var balanceModels = await storage.LoadBalances();

            var accountsCount = accountModels.Count;
            var accounts = new List<Account>();
            foreach (var account in accountModels)
            {
                var currentAccountBalanceFromCursor = BalanceModelIdConverter.EncodeId(account.Id, 0);
                var currentAccountBalanceToCursor = BalanceModelIdConverter.EncodeId(account.Id + 1, 0);
                var balances = balanceModels
                    .SkipWhile(b => b.Id < currentAccountBalanceFromCursor)
                    .TakeWhile(b => b.Id < currentAccountBalanceToCursor).ToArray();
                accounts.Add(account.ToAccount(balances));
            }
            return accounts;
        }

        private async Task<Dictionary<PaymentProvider, WithdrawalStorage>> GetWithdrawals(AccountStorage accountStorage, ConstellationSettings constellationSettings)
        {
            var result = new Dictionary<PaymentProvider, WithdrawalStorage>();
            var withdrawalApexes = accountStorage.GetAll().Where(a => a.Account.Withdrawal != default).Select(a => a.Account.Withdrawal).ToArray();
            if (withdrawalApexes.Length < 1)
                return result;

            var withdrawalQuanta = await storage.LoadQuanta(withdrawalApexes);

            foreach (var w in withdrawalQuanta.OrderBy(w => w.Apex))
            {
                var withdrawalQuantum = XdrConverter.Deserialize<MessageEnvelope>(w.Bin);
                var withdrawalRequest = ((WithdrawalRequest)((RequestQuantum)withdrawalQuantum.Message).RequestMessage);
                withdrawalQuantum.TryAssignAccountWrapper(accountStorage);

                if (!PaymentsParserManager.TryGetParser(withdrawalRequest.PaymentProvider, out var parser))
                    throw new Exception($"Unable to find parser for {withdrawalRequest.PaymentProvider} provider.");

                if (!parser.TryDeserializeTransaction(withdrawalRequest.TransactionXdr, out var transactionWrapper))
                    throw new Exception($"Invalid {withdrawalRequest.PaymentProvider} withdrawal bin.");

                var vault = constellationSettings.Vaults.FirstOrDefault(v => v.Provider == withdrawalRequest.PaymentProvider);
                if (vault == null)
                    throw new Exception($"Unable to find vault for {withdrawalRequest.PaymentProvider} provider.");

                var withdrawal = parser.GetWithdrawal(withdrawalQuantum, transactionWrapper, constellationSettings, vault.AccountId.ToString());

                if (!result.TryGetValue(withdrawalRequest.PaymentProvider, out var storage))
                    storage.Add(withdrawal);
            }

            return result;
        }

        private async Task<List<Order>> GetOrders(AccountStorage accountStorage)
        {
            var orderModels = await storage.LoadOrders();

            var orders = new List<Order>();
            var orderLength = orderModels.Count;
            for (int i = 0; i < orderLength; i++)
            {
                orders.Add(orderModels[i].ToOrder(accountStorage));
            }

            return orders;
        }
    }
}
