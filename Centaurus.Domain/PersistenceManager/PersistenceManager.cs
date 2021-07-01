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
using Centaurus.PaymentProvider;
using Centaurus.Domain.Models;

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
            var accountQuanta = await storage.LoadEffects(apex, isDesc, limit, account);
            var accountEffects = new List<ApexEffects>();
            foreach (var quantum in accountQuanta.OrderBy(e => e.Apex))
            {
                var quantumContainer = quantum.ToQuantumContainer();
                accountEffects.Add(new ApexEffects
                {
                    Apex = ((Quantum)quantumContainer.Quantum.Message).Apex,
                    Items = quantumContainer.Effects.Where(a => a.Account == account).ToList(),
                    Proof = quantumContainer.EffectsProof
                });
            }

            if (isDesc)
            {
                accountEffects.Reverse();
                accountEffects.ForEach(ae => ae.Items.Reverse());
            }
            return new EffectsResponse
            {
                CurrentPagingToken = rawCursor,
                Order = isDesc ? EffectsRequest.Desc : EffectsRequest.Asc,
                Items = accountEffects,
                NextPageToken = accountEffects.LastOrDefault()?.Apex.ToString(),
                PrevPageToken = accountEffects.FirstOrDefault()?.Apex.ToString(),
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
        /// Creates snapshot from effect
        /// </summary>
        /// <returns></returns>
        public static Snapshot GetSnapshot(ConstellationInitEffect constellationInitEffect, byte[] quantumHash)
        {
            var snapshot = new Snapshot
            {
                Apex = constellationInitEffect.Apex,
                Accounts = new List<AccountWrapper>(),
                Orders = new List<OrderWrapper>(),
                Settings = new ConstellationSettings
                {
                    Apex = constellationInitEffect.Apex,
                    Assets = constellationInitEffect.Assets,
                    Auditors = constellationInitEffect.Auditors,
                    MinAccountBalance = constellationInitEffect.MinAccountBalance,
                    MinAllowedLotSize = constellationInitEffect.MinAllowedLotSize,
                    Providers = constellationInitEffect.Providers,
                    RequestRateLimits = constellationInitEffect.RequestRateLimits
                },
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

            var orders = await GetOrders(accountStorage);

            var exchange = await GetRestoredExchange(orders);

            var batchSize = 1000;
            var effects = new List<Effect>();
            while (true)
            {
                var quanta = await storage.LoadQuantaAboveApex(apex, batchSize);
                effects.AddRange(quanta.SelectMany(q => q.ToQuantumContainer().Effects));
                if (quanta.Count < batchSize)
                    break;
            }

            for (var i = effects.Count - 1; i >= 0; i--)
            {
                var currentEffect = effects[i];

                var account = accountStorage.GetAccount(currentEffect.Account);
                IEffectProcessor<Effect> processor = null;
                switch (currentEffect)
                {
                    case AccountCreateEffect accountCreateEffect:
                        processor = new AccountCreateEffectProcessor(accountCreateEffect, accountStorage, settings.RequestRateLimits);
                        break;
                    case NonceUpdateEffect nonceUpdateEffect:
                        processor = new NonceUpdateEffectProcessor(nonceUpdateEffect, account);
                        break;
                    case BalanceCreateEffect balanceCreateEffect:
                        processor = new BalanceCreateEffectProcessor(balanceCreateEffect, account);
                        break;
                    case BalanceUpdateEffect balanceUpdateEffect:
                        processor = new BalanceUpdateEffectProcesor(balanceUpdateEffect, account);
                        break;
                    case RequestRateLimitUpdateEffect requestRateLimitUpdateEffect:
                        processor = new RequestRateLimitUpdateEffectProcessor(requestRateLimitUpdateEffect, account, settings.RequestRateLimits);
                        break;
                    case OrderPlacedEffect orderPlacedEffect:
                        {
                            var orderBook = exchange.GetOrderbook(orderPlacedEffect.OrderId);
                            var order = exchange.OrderMap.GetOrder(orderPlacedEffect.OrderId);
                            processor = new OrderPlacedEffectProcessor(orderPlacedEffect, order.AccountWrapper, orderBook, order);
                        }
                        break;
                    case OrderRemovedEffect orderRemovedEffect:
                        {
                            var orderBook = exchange.GetOrderbook(orderRemovedEffect.OrderId);
                            processor = new OrderRemovedEffectProccessor(orderRemovedEffect, accountStorage.GetAccount(orderRemovedEffect.Account), orderBook);
                        }
                        break;
                    case TradeEffect tradeEffect:
                        {
                            var order = exchange.OrderMap.GetOrder(tradeEffect.OrderId);
                            if (order == null) //no need to revert trade if no order was created
                                continue;
                            processor = new TradeEffectProcessor(tradeEffect, accountStorage.GetAccount(tradeEffect.Account), order);
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
                Orders = allOrders.OrderBy(o => o.OrderId).ToList(),
                Settings = settings,
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

        private async Task<Exchange> GetRestoredExchange(List<OrderWrapper> orders)
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

        private async Task<List<OrderWrapper>> GetOrders(AccountStorage accountStorage)
        {
            var orderModels = await storage.LoadOrders();

            var orders = new List<OrderWrapper>();
            foreach (var order in orderModels)
                orders.Add(order.ToOrder(accountStorage.GetAccount(order.Account)));

            return orders;
        }
    }
}
