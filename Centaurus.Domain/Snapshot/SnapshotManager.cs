using Centaurus.DAL;
using Centaurus.DAL.Models;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
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

        public static async Task<Snapshot> BuildGenesisSnapshot(ConstellationSettings settings, long ledger, long vaultSequence)
        {
            var initEffect = new ConstellationInitEffect
            {
                Assets = settings.Assets,
                Auditors = settings.Auditors,
                MinAccountBalance = settings.MinAccountBalance,
                MinAllowedLotSize = settings.MinAllowedLotSize,
                Vault = settings.Vault,
                VaultSequence = vaultSequence,
                Ledger = ledger
            };

            var updates = new PendingUpdates();
            updates.Add(null, new Effect[] { initEffect });

            await SaveSnapshotInternal(updates);

            var snapshot = new Snapshot();
            snapshot.Ledger = ledger;
            snapshot.Withdrawals = new List<Withdrawal>();
            snapshot.Accounts = new List<Account>();
            snapshot.Orders = new List<Order>();
            snapshot.Settings = settings;
            snapshot.VaultSequence = vaultSequence;

            return snapshot;
        }

        //only one thread can save snapshots. We need make sure that previous snapshot is permanent
        public async Task SaveSnapshot(PendingUpdates updates)
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

        private Action onSnapshotSuccess;
        private Action<string> onSnapshotFailed;


        private static SemaphoreSlim saveSnapshotSemaphore = new SemaphoreSlim(1);

        private static async Task SaveSnapshotInternal(PendingUpdates updates)
        {
            await saveSnapshotSemaphore.WaitAsync();
            try
            {
                var aggregatedUpdates = await UpdatesAggregator.Aggregate(updates.GetAll());
                await Global.PermanentStorage.Update(aggregatedUpdates);
            }
            finally
            {
                saveSnapshotSemaphore.Release();
            }
        }

        /// <summary>
        /// Fetches current snapshot
        /// </summary>
        /// <returns></returns>
        public static async Task<Snapshot> GetSnapshot()
        {
            //if we pass ulong.MaxValue it will return current snapshot
            return await GetSnapshot(ulong.MaxValue);
        }

        /// <summary>
        /// Builds snapshot for specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public static async Task<Snapshot> GetSnapshot(ulong apex)
        {
            var settings = await GetConstellationSettings(apex);
            if (settings == null)
                return null;

            var snapshotApex = apex != ulong.MaxValue ? apex : await Global.PermanentStorage.GetLastApex();

            var stellarData = await Global.PermanentStorage.LoadStellarData();

            var accounts = await GetAccounts();

            var withdrawals = await GetWithdrawals();

            var orders = await GetOrders();

            var accountStorage = new AccountStorage(accounts);

            var exchange = await GetRestoredExchange(orders);

            var withdrawalsStorage = new WithdrawalStorage(withdrawals);

            var effectModels = await Global.PermanentStorage.LoadEffectsAboveApex(snapshotApex);
            for (var i = effectModels.Count - 1; i >= 0; i--)
            {
                var currentEffect = XdrConverter.Deserialize<Effect>(effectModels[i].RawEffect);
                var pubKey = currentEffect.Pubkey;
                var currentAccount = new Lazy<Account>(() => accountStorage.GetAccount(pubKey));
                IEffectProcessor<Effect> processor = null;
                switch (currentEffect)
                {
                    case AccountCreateEffect accountCreateEffect:
                        processor = new AccountCreateEffectProcessor(accountCreateEffect, accountStorage);
                        break;
                    case NonceUpdateEffect nonceUpdateEffect:
                        processor = new NonceUpdateEffectProcessor(nonceUpdateEffect, currentAccount.Value);
                        break;
                    case BalanceCreateEffect balanceCreateEffect:
                        processor = new BalanceCreateEffectProcessor(balanceCreateEffect, accountStorage);
                        break;
                    case BalanceUpdateEffect balanceUpdateEffect:
                        processor = new BalanceUpdateEffectProcesor(balanceUpdateEffect, accountStorage);
                        break;
                    case LockLiabilitiesEffect lockLiabilitiesEffect:
                        processor = new LockLiabilitiesEffectProcessor(lockLiabilitiesEffect, accountStorage);
                        break;
                    case UnlockLiabilitiesEffect unlockLiabilitiesEffect:
                        processor = new UnlockLiabilitiesEffectProcessor(unlockLiabilitiesEffect, accountStorage);
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
                            processor = new TradeEffectProcessor(tradeEffect, order);
                        }
                        break;
                    case WithdrawalCreateEffect withdrawalCreate:
                        {
                            processor = new WithdrawalCreateEffectProcessor(withdrawalCreate, withdrawalsStorage);
                        }
                        break;
                    case WithdrawalRemoveEffect withdrawalRemove:
                        {
                            processor = new WithdrawalRemoveEffectProcessor(withdrawalRemove, withdrawalsStorage);
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }

                processor.RevertEffect();
            }

            return new Snapshot
            {
                Apex = snapshotApex,
                Accounts = accountStorage.GetAll().ToList(),
                Ledger = stellarData.Ledger,
                Orders = exchange.OrderMap.GetAllOrders().ToList(),
                Settings = settings,
                VaultSequence = stellarData.VaultSequence,
                Withdrawals = withdrawals
            };
        }

        private static async Task<Exchange> GetRestoredExchange(List<Order> orders)
        {
            var assets = await Global.PermanentStorage.LoadAssets(ulong.MaxValue);//we need to load all assets, otherwise errors could occur during exchange restore
            return Exchange.RestoreExchange(assets.Select(a => a.ToAssetSettings()).ToList(), orders);
        }

        private static async Task<ConstellationSettings> GetConstellationSettings(ulong apex)
        {
            var settingsModel = await Global.PermanentStorage.LoadSettings(apex);
            if (settingsModel == null)
                return null;

            var assets = await Global.PermanentStorage.LoadAssets(apex);

            return settingsModel.ToSettings(assets);
        }

        private static async Task<List<Account>> GetAccounts()
        {
            var accountModels = await Global.PermanentStorage.LoadAccounts();
            var balanceModels = await Global.PermanentStorage.LoadBalances();

            var comparer = new ByteArrayComparer();
            var groupedBalances = balanceModels.GroupBy(b => b.Account, comparer).ToDictionary(g => g.Key, g => g, comparer);
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

        private static async Task<List<Withdrawal>> GetWithdrawals()
        {
            var withdrawalModels = await Global.PermanentStorage.LoadWithdrawals();

            return withdrawalModels.Select(w => XdrConverter.Deserialize<Withdrawal>(w.RawWithdrawal)).ToList();
        }

        private static async Task<List<Order>> GetOrders()
        {
            var orderModels = await Global.PermanentStorage.LoadOrders();

            var orders = new List<Order>();
            var orderLength = orderModels.Count;
            for (int i = 0; i < orderLength; i++)
            {
                orders.Add(orderModels[i].ToOrder());
            }

            return orders;
        }
    }
}
