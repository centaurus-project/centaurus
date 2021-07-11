using Centaurus.DAL;
using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.PersistentStorage.Abstraction;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    //TODO: rename and separate it.
    public class PersistenceManager : ContextualBase
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private SemaphoreSlim saveSnapshotSemaphore = new SemaphoreSlim(1);

        public PersistenceManager(ExecutionContext context)
            : base(context)
        {
        }

        public EffectsResponse LoadEffects(string rawCursor, bool isDesc, int limit, ulong account)
        {
            if (string.IsNullOrEmpty(rawCursor))
                rawCursor = "0";
            if (!ulong.TryParse(rawCursor, out var apex))
                throw new ArgumentException("Cursor is invalid.");
            var order = isDesc ? PersistentStorage.QueryResultsOrder.Desc : PersistentStorage.QueryResultsOrder.Asc;
            var accountQuanta = Context.PermanentStorage.LoadQuantaForAccount(account, apex, limit, order);
            var accountEffects = new List<ApexEffects>();
            foreach (var quantum in accountQuanta)
            {
                accountEffects.Add(new ApexEffects
                {
                    Apex = quantum.Apex,
                    Items = quantum.Effects.Select(e => XdrConverter.Deserialize<Effect>(e)).Where(a => a.Account == account).ToList(),
                    Proof = new EffectsProof
                    {
                        Hashes = new EffectHashes { Hashes = quantum.Proof.EffectHashes.Select(h => new Hash { Data = h }).ToList() },
                        Signatures = quantum.Proof.Signatures.Select(s => new Ed25519Signature { Signer = s.Signer, Signature = s.Data }).ToList()
                    }
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

        public void ApplyUpdates(DiffObject updates)
        {
            saveSnapshotSemaphore.Wait();
            try
            {
                var updateModels = updates.Batch.ToList();
                updateModels.AddRange(
                    updates.Accounts.Select(a => Context.AccountStorage.GetAccount(a).Account.ToPersistentModel()).Cast<IPersistentModel>().ToList()
                );
                updateModels.AddRange(
                    updates.Cursors.Select(p => Context.PaymentProvidersManager.GetManager(p).ToPersistentModel()).Cast<IPersistentModel>().ToList()
                );
                Context.PermanentStorage.SaveBatch(updateModels);
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
        public List<MessageEnvelope> GetQuantaAboveApex(ulong apex, int count = 0)
        {
            var quantaModels = Context.PermanentStorage.LoadQuantaAboveApex(apex);
            var query = (IEnumerable<QuantumPersistentModel>)quantaModels.OrderBy(q => q.Apex);
            if (count > 0)
                query = query.Take(count);
            return query.Select(q => XdrConverter.Deserialize<MessageEnvelope>(q.RawQuantum)).ToList();
        }

        public ulong GetLastApex()
        {
            return Context.PermanentStorage.GetLastApex();
        }

        public MessageEnvelope GetQuantum(ulong apex)
        {
            var quantumModel = Context.PermanentStorage.LoadQuantum(apex);
            return XdrConverter.Deserialize<MessageEnvelope>(quantumModel.RawQuantum);
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
                Cursors = constellationInitEffect.Providers.ToDictionary(k => k.ProviderId, v => v.InitCursor),
                LastHash = quantumHash
            };
            return snapshot;
        }

        /// <summary>
        /// Fetches settings for the specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public ConstellationSettings GetConstellationSettings(ulong apex)
        {
            var settingsModel = Context.PermanentStorage.LoadSettings(apex);
            if (settingsModel == null)
                return null;

            return settingsModel.ToDomainModel();
        }

        /// <summary>
        /// Builds snapshot for specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public Snapshot GetSnapshot(ulong apex)
        {
            if (apex < 0)
                throw new ArgumentException("Apex cannot be less than zero.");

            var lastApex = GetLastApex();
            if (lastApex < apex)
                throw new InvalidOperationException("Requested apex is greater than the last known one.");

            //some auditors can have capped db
            var minRevertApex = GetMinRevertApex();
            if (minRevertApex == 0 && apex != lastApex || apex < minRevertApex)
                throw new InvalidOperationException($"Lack of data to revert to {apex} apex.");

            var settings = GetConstellationSettings(apex);
            if (settings == null)
                return null;

            var cursors = Context.PermanentStorage.LoadCursors();

            var accounts = (GetAccounts(settings.GetBaseAsset())).Select(a => new AccountWrapper(a, settings.RequestRateLimits));

            var accountStorage = new AccountStorage(accounts);

            var orders = accounts.SelectMany(a => a.Account.Orders.Select(o => new OrderWrapper(o, a))).OrderBy(o => o.Order.OrderId).ToList();
            var exchange = GetRestoredExchange(orders);

            var quanta = Context.PermanentStorage.LoadQuantaAboveApex(apex);

            var effects = quanta.SelectMany(q => q.Effects.Select(e => XdrConverter.Deserialize<Effect>(e))).ToList();

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
                        processor = new BalanceUpdateEffectProcesor(balanceUpdateEffect, account, balanceUpdateEffect.Sign);
                        break;
                    case RequestRateLimitUpdateEffect requestRateLimitUpdateEffect:
                        processor = new RequestRateLimitUpdateEffectProcessor(requestRateLimitUpdateEffect, account, settings.RequestRateLimits);
                        break;
                    case OrderPlacedEffect orderPlacedEffect:
                        {
                            var orderBook = exchange.GetOrderbook(orderPlacedEffect.Asset, orderPlacedEffect.Side);
                            var order = exchange.OrderMap.GetOrder(orderPlacedEffect.Apex);
                            processor = new OrderPlacedEffectProcessor(orderPlacedEffect, order.AccountWrapper, orderBook, order, settings.GetBaseAsset());
                        }
                        break;
                    case OrderRemovedEffect orderRemovedEffect:
                        {
                            var orderBook = exchange.GetOrderbook(orderRemovedEffect.Asset, orderRemovedEffect.Side);
                            processor = new OrderRemovedEffectProccessor(orderRemovedEffect, accountStorage.GetAccount(orderRemovedEffect.Account), orderBook, settings.GetBaseAsset());
                        }
                        break;
                    case TradeEffect tradeEffect:
                        {
                            var order = exchange.OrderMap.GetOrder(tradeEffect.Apex);
                            if (order == null) //no need to revert trade if no order was created
                                continue;
                            processor = new TradeEffectProcessor(tradeEffect, order.AccountWrapper, order, settings.GetBaseAsset());
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }

                processor.RevertEffect();
            }

            var lastQuantumData = XdrConverter.Deserialize<MessageEnvelope>(Context.PermanentStorage.LoadQuantum(apex).RawQuantum);

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
                LastHash = lastQuantumData.Message.ComputeHash()
            };
        }

        /// <summary>
        /// Returns minimal apex a snapshot can be reverted to
        /// </summary>
        /// <returns></returns>
        public ulong GetMinRevertApex()
        {
            //obtain min apex we can revert to
            var minApex = Context.PermanentStorage.LoadQuanta().FirstOrDefault()?.Apex ?? 0;
            if (minApex == 0) //we can't revert at all
                return 0;

            return minApex - 1; //we can revert effect for that apex, so the minimal apex is first effect apex - 1
        }

        private Exchange GetRestoredExchange(List<OrderWrapper> orders)
        {
            var settings = GetConstellationSettings(ulong.MaxValue); // load last settings
            return Exchange.RestoreExchange(settings.Assets, orders, false);
        }

        private List<Account> GetAccounts(string baseAsset)
        {
            return Context.PermanentStorage.LoadAccounts()
                .Select(a => a.ToDomainModel(baseAsset))
                .ToList();
        }
    }
}
