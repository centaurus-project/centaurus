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

        public object Stop { get; internal set; }

        public PersistenceManager(IStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public async Task<EffectsResponse> LoadEffects(string rawCursor, bool isDesc, int limit, int account)
        {
            if (account == default)
                throw new ArgumentNullException(nameof(account));

            //var cursor = ByteArrayExtensions.FromHexString(rawCursor);
            //if (cursor != null && cursor.Length != 12)
            //    throw new ArgumentException("Cursor is invalid.");
            if (!long.TryParse(rawCursor, out var apex))
                throw new ArgumentException("Cursor is invalid.");
            var accountEffectModels = (await storage.LoadEffects(apex, isDesc, limit, account));
            var effectModels = accountEffectModels
                .OrderBy(e => e.Apex)
                .SelectMany(ae => ae.Effects.Select(e => e.ToEffect(account)))
                .ToList();
            if (isDesc)
                effectModels.Reverse();
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

        public async Task ApplyUpdates(DiffObject updates)
        {
            await saveSnapshotSemaphore.WaitAsync();
            try
            {
                await storage.Update(updates);
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
            return quantaModels.OrderBy(q => q.Apex).Select(q => XdrConverter.Deserialize<MessageEnvelope>(q.RawQuantum)).ToList();
        }

        public async Task<long> GetLastApex()
        {
            return await storage.GetLastApex();
        }

        public async Task<MessageEnvelope> GetQuantum(long apex)
        {
            var quantumModel = await storage.LoadQuantum(apex);
            return XdrConverter.Deserialize<MessageEnvelope>(quantumModel.RawQuantum);
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
                Accounts = new List<Account>(),
                Orders = new List<Order>(),
                Withdrawals = new List<WithdrawalWrapper>(),
                Settings = new ConstellationSettings
                {
                    Apex = constellationInitEffect.Apex,
                    Assets = assets,
                    Auditors = constellationInitEffect.Auditors,
                    MinAccountBalance = constellationInitEffect.MinAccountBalance,
                    MinAllowedLotSize = constellationInitEffect.MinAllowedLotSize,
                    Vault = constellationInitEffect.Vault,
                    RequestRateLimits = constellationInitEffect.RequestRateLimits
                },
                TxCursor = constellationInitEffect.TxCursor,
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

            var assets = await storage.LoadAssets(apex);

            return settingsModel.ToSettings(assets);
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

            var stellarData = await storage.LoadConstellationState();

            var accounts = await GetAccounts();

            var accountStorage = new AccountStorage(accounts, settings.RequestRateLimits);

            var withdrawals = await GetWithdrawals(accountStorage, settings);

            var orders = await GetOrders(accountStorage);

            var exchange = await GetRestoredExchange(orders);

            var withdrawalsStorage = new WithdrawalStorage(withdrawals, false);

            var accountEffectModels = await storage.LoadEffectsAboveApex(apex);

            var effectModels = new Effect[accountEffectModels.Sum(a => a.Effects.Count)];
            var currentApexIndexOffset = 0;
            foreach (var quantumEffect in accountEffectModels.GroupBy(a => a.Apex))
            {
                var effectsCount = 0;
                foreach (var accountEffects in quantumEffect)
                {
                    foreach (var rawEffect in accountEffects.Effects)
                    {
                        var effect = rawEffect.ToEffect(accountEffects.Account);
                        effectModels[currentApexIndexOffset + rawEffect.ApexIndex] = effect;
                        effectsCount++;
                    }
                }
                currentApexIndexOffset += effectsCount;
            }
            for (var i = effectModels.Length - 1; i >= 0; i--)
            {
                var currentEffect = effectModels[i];
                var accountId = currentEffect.Account;
                var account = new Lazy<AccountWrapper>(() => accountStorage.GetAccount(accountId));
                IEffectProcessor<Effect> processor = null;
                switch (currentEffect)
                {
                    case AccountCreateEffect accountCreateEffect:
                        processor = new AccountCreateEffectProcessor(accountCreateEffect, accountStorage);
                        break;
                    case NonceUpdateEffect nonceUpdateEffect:
                        processor = new NonceUpdateEffectProcessor(nonceUpdateEffect, account.Value.Account);
                        break;
                    case BalanceCreateEffect balanceCreateEffect:
                        processor = new BalanceCreateEffectProcessor(balanceCreateEffect, account.Value.Account);
                        break;
                    case BalanceUpdateEffect balanceUpdateEffect:
                        processor = new BalanceUpdateEffectProcesor(balanceUpdateEffect, account.Value.Account);
                        break;
                    case UpdateLiabilitiesEffect updateLiabilitiesEffect:
                        processor = new UpdateLiabilitiesEffectProcessor(updateLiabilitiesEffect, account.Value.Account);
                        break;
                    case RequestRateLimitUpdateEffect requestRateLimitUpdateEffect:
                        processor = new RequestRateLimitUpdateEffectProcessor(requestRateLimitUpdateEffect, account.Value, settings.RequestRateLimits);
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
                            processor = new OrderRemovedEffectProccessor(orderRemovedEffect, orderBook, account.Value.Account);
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
                            var withdrawal = withdrawalsStorage.GetWithdrawal(withdrawalCreate.Apex);
                            processor = new WithdrawalCreateEffectProcessor(withdrawalCreate, withdrawal, withdrawalsStorage);
                        }
                        break;
                    case WithdrawalRemoveEffect withdrawalRemove:
                        {
                            var withdrawal = withdrawalsStorage.GetWithdrawal(withdrawalRemove.Apex);
                            processor = new WithdrawalRemoveEffectProcessor(withdrawalRemove, withdrawal, withdrawalsStorage);
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }

                processor.RevertEffect();
            }

            var lastQuantum = (await storage.LoadQuantum(apex)).ToMessageEnvelope();

            return new Snapshot
            {
                Apex = apex,
                Accounts = accountStorage.GetAll().OrderBy(a => a.Account.Id).Select(a => a.Account).ToList(),
                TxCursor = stellarData?.TxCursor ?? 0,
                Orders = exchange.OrderMap.GetAllOrders().OrderBy(o => o.OrderId).ToList(),
                Settings = settings,
                Withdrawals = withdrawalsStorage.GetAll().OrderBy(w => w.Apex).ToList(),
                LastHash = lastQuantum.Message.ComputeHash()
            };
        }

        /// <summary>
        /// Returns minimal apex a snapshot can be reverted to
        /// </summary>
        /// <returns></returns>
        public async Task<long> GetMinRevertApex()
        {
            //obtain min apex we can revert to
            var minApex = await storage.GetFirstEffectApex();
            if (minApex == -1) //we can't revert at all
                return -1;

            return minApex - 1; //we can revert effect for that apex, so the minimal apex is first effect apex - 1
        }

        private async Task<Exchange> GetRestoredExchange(List<Order> orders)
        {
            var assets = await storage.LoadAssets(long.MaxValue);//we need to load all assets, otherwise errors could occur during exchange restore
            return Exchange.RestoreExchange(assets.Select(a => a.ToAssetSettings()).OrderBy(a => a.Id).ToList(), orders, false);
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

        private async Task<List<WithdrawalWrapper>> GetWithdrawals(AccountStorage accountStorage, ConstellationSettings constellationSettings)
        {
            var withdrawalApexes = accountStorage.GetAll().Where(a => a.Account.Withdrawal != default).Select(a => a.Account.Withdrawal).ToArray();
            if (withdrawalApexes.Length < 1)
                return new List<WithdrawalWrapper>();
            var withdrawalQuanta = await storage.LoadQuanta(withdrawalApexes);
            var withdrawals = withdrawalQuanta
                .Select(w =>
                {
                    var withdrawalQuantum = XdrConverter.Deserialize<MessageEnvelope>(w.RawQuantum);
                    var withdrawalRequest = ((WithdrawalRequest)((RequestQuantum)withdrawalQuantum.Message).RequestMessage);
                    withdrawalQuantum.TryAssignAccountWrapper(accountStorage);
                    return WithdrawalWrapperExtensions.GetWithdrawal(withdrawalQuantum, constellationSettings);
                });

            return withdrawals.OrderBy(w => w.Apex).ToList();
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
