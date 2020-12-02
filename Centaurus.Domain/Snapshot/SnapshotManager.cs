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

namespace Centaurus.Domain
{
    //TODO: rename and separate it.
    public class SnapshotManager
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Initiates snapshot manager
        /// </summary>
        /// <param name="_onSnapshotSuccess">The delegate that is called when the snapshot is successful</param>
        /// <param name="_onSnapshotFailed">The delegate that is called when the snapshot is failed</param>
        public SnapshotManager(Action _onSnapshotSuccess, Action<string> _onSnapshotFailed)
        {
            onSnapshotSuccess = _onSnapshotSuccess;
            onSnapshotFailed = _onSnapshotFailed;
        }

        /// <summary>
        /// Makes initial save to DB
        /// </summary>
        /// <param name="envelope">Envelope that contains constellation init quantum.</param>
        public static async Task<Effect[]> ApplyInitUpdates(MessageEnvelope envelope)
        {

            var initQuantum = (ConstellationInitQuantum)envelope.Message;

            var initEffects = new Effect[] {
                new ConstellationInitEffect {
                    Apex = initQuantum.Apex,
                    Assets = initQuantum.Assets,
                    Auditors = initQuantum.Auditors,
                    MinAccountBalance = initQuantum.MinAccountBalance,
                    MinAllowedLotSize = initQuantum.MinAllowedLotSize,
                    Vault = initQuantum.Vault,
                    RequestRateLimits = initQuantum.RequestRateLimits
                }
            };

            var updates = new PendingUpdates();
            updates.Add(envelope, initEffects);

            await SaveSnapshotInternal(updates);

            return initEffects;
        }

        //only one thread can save snapshots. We need make sure that previous snapshot is permanent
        public async Task ApplyUpdates(PendingUpdates updates)
        {
            try
            {
                await SaveSnapshotInternal(updates);
                onSnapshotSuccess();
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Unable to save snapshot");
                onSnapshotFailed?.Invoke("Unable to save snapshot");
            }
        }

        public async Task<EffectsResponse> LoadEffects(string rawCursor, bool isDesc, int limit, byte[] account)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));

            var cursor = ByteArrayExtensions.FromHexString(rawCursor);
            if (cursor != null && cursor.Length != 12)
                throw new ArgumentException("Cursor is invalid.");
            var effectModels = await Global.PermanentStorage.LoadEffects(cursor, isDesc, limit, account);
            return new EffectsResponse
            {
                CurrentPagingToken = rawCursor,
                Order = isDesc ? EffectsRequest.Desc : EffectsRequest.Asc,
                Items = effectModels.Select(e => e.ToEffect()).ToList(),
                NextPageToken = (effectModels.LastOrDefault()?.Id).ToHex(),
                PrevPageToken = (effectModels.FirstOrDefault()?.Id).ToHex(),
                Limit = limit
            };
        }

        private Action onSnapshotSuccess;
        private Action<string> onSnapshotFailed;


        private static SemaphoreSlim saveSnapshotSemaphore = new SemaphoreSlim(1);

        private static async Task SaveSnapshotInternal(PendingUpdates updates)
        {
            await saveSnapshotSemaphore.WaitAsync();
            try
            {
                var updateItems = updates.GetAll();
                if (updateItems.Count > 0)
                {
                    var aggregatedUpdates = await UpdatesAggregator.Aggregate(updateItems);
                    await Global.PermanentStorage.Update(aggregatedUpdates);
                }
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
        public static async Task<List<MessageEnvelope>> GetQuantaAboveApex(long apex, int count = 0)
        {
            var quantaModels = await Global.PermanentStorage.LoadQuantaAboveApex(apex, count);
            return quantaModels.Select(q => XdrConverter.Deserialize<MessageEnvelope>(q.RawQuantum)).ToList();
        }

        /// <summary>
        /// Builds updates based on the snapshot, and saves it to DB
        /// </summary>
        public static async Task InitFromSnapshot(Snapshot snapshot)
        {
            if (Global.AppState.State != ApplicationState.WaitingForInit)
                throw new InvalidOperationException("This operation is only permitted when the app is in WaitingForInit state");
            var aggregatedUpdates = UpdatesAggregator.Aggregate(snapshot);
            await Global.PermanentStorage.Update(aggregatedUpdates);
        }

        public static async Task<long> GetLastApex()
        {
            return await Global.PermanentStorage.GetLastApex();
        }

        public static async Task<MessageEnvelope> GetQuantum(long apex)
        {
            var quantumModel = await Global.PermanentStorage.LoadQuantum(apex);
            return XdrConverter.Deserialize<MessageEnvelope>(quantumModel.RawQuantum);
        }

        /// <summary>
        /// Fetches current snapshot
        /// </summary>
        /// <returns></returns>
        public static async Task<Snapshot> GetSnapshot()
        {
            var lastApex = await GetLastApex();
            if (lastApex < 0)
                return null;
            return await GetSnapshot(lastApex);
        }

        /// <summary>
        /// Fetches settings for the specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public static async Task<ConstellationSettings> GetConstellationSettings(long apex)
        {
            var settingsModel = await Global.PermanentStorage.LoadSettings(apex);
            if (settingsModel == null)
                return null;

            var assets = await Global.PermanentStorage.LoadAssets(apex);

            return settingsModel.ToSettings(assets);
        }

        /// <summary>
        /// Builds snapshot for specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public static async Task<Snapshot> GetSnapshot(long apex)
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

            var lastQuantum = (await Global.PermanentStorage.LoadQuantum(apex)).ToMessageEnvelope();

            var settings = await GetConstellationSettings(apex);
            if (settings == null)
                return null;

            var stellarData = await Global.PermanentStorage.LoadConstellationState();

            var accounts = await GetAccounts();

            var accountStorage = new AccountStorage(accounts, settings.RequestRateLimits);

            var withdrawals = await GetWithdrawals(accountStorage, settings);

            var orders = await GetOrders(accountStorage);

            var exchange = await GetRestoredExchange(orders);

            var withdrawalsStorage = new WithdrawalStorage(withdrawals, false);

            var effectModels = await Global.PermanentStorage.LoadEffectsAboveApex(apex);
            for (var i = effectModels.Count - 1; i >= 0; i--)
            {
                var currentEffect = XdrConverter.Deserialize<Effect>(effectModels[i].RawEffect);
                var pubKey = currentEffect.Pubkey;
                var account = new Lazy<AccountWrapper>(() => accountStorage.GetAccount(pubKey));
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

            return new Snapshot
            {
                Apex = apex,
                Accounts = accountStorage.GetAll().Select(a => a.Account).ToList(),
                TxCursor = stellarData?.TxCursor ?? 0,
                Orders = exchange.OrderMap.GetAllOrders().ToList(),
                Settings = settings,
                Withdrawals = withdrawals,
                LastHash = lastQuantum.Message.ComputeHash()
            };
        }

        /// <summary>
        /// Returns minimal apex a snapshot can be reverted to
        /// </summary>
        /// <returns></returns>
        public static async Task<long> GetMinRevertApex()
        {
            //obtain min apex we can revert to
            var minApex = await Global.PermanentStorage.GetFirstEffectApex();
            if (minApex == -1) //we can't revert at all
                return -1;

            return minApex - 1; //we can revert effect for that apex, so the minimal apex is first effect apex - 1
        }

        private static async Task<Exchange> GetRestoredExchange(List<Order> orders)
        {
            var assets = await Global.PermanentStorage.LoadAssets(long.MaxValue);//we need to load all assets, otherwise errors could occur during exchange restore
            return Exchange.RestoreExchange(assets.Select(a => a.ToAssetSettings()).ToList(), orders, false);
        }

        private static async Task<List<Account>> GetAccounts()
        {
            var accountModels = await Global.PermanentStorage.LoadAccounts();
            var balanceModels = await Global.PermanentStorage.LoadBalances();

            var groupedBalances = balanceModels.GroupBy(b => b.Account, ByteArrayComparer.Default).ToDictionary(g => g.Key, g => g, ByteArrayComparer.Default);
            var accountsCount = accountModels.Count;
            var accounts = new List<Account>();
            for (var i = 0; i < accountsCount; i++)
            {
                var acc = accountModels[i];
                var balances = new BalanceModel[] { };
                if (groupedBalances.ContainsKey(acc.PubKey))
                {
                    balances = groupedBalances[acc.PubKey].ToArray();
                }
                accounts.Add(acc.ToAccount(balances));
            }

            return accounts;
        }

        private static async Task<List<Withdrawal>> GetWithdrawals(AccountStorage accountStorage, ConstellationSettings constellationSettings)
        {
            var withdrawalQuanta = await Global.PermanentStorage.LoadWithdrawals();
            var withdrawals = withdrawalQuanta
                .Select(w =>
                {
                    var withdrawalQuantum = XdrConverter.Deserialize<MessageEnvelope>(w.RawQuantum);
                    var withdrawalRequest = ((WithdrawalRequest)((RequestQuantum)withdrawalQuantum.Message).RequestMessage);
                    withdrawalRequest.AccountWrapper = accountStorage.GetAccount(w.Account);
                    return Withdrawal.GetWithdrawal(withdrawalQuantum, constellationSettings);
                });

            return withdrawals.ToList();
        }

        private static async Task<List<Order>> GetOrders(AccountStorage accountStorage)
        {
            var orderModels = await Global.PermanentStorage.LoadOrders();

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
