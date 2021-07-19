using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    //TODO: rename and separate it.
    public class PersistenceManager : ContextualBase
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private object syncRoot = new { };

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
            var order = isDesc ? QueryResultsOrder.Desc : QueryResultsOrder.Asc;
            var accountQuanta = Context.PermanentStorage.LoadQuantaForAccount(account, apex, limit, order);
            var accountEffects = new List<ApexEffects>();

            foreach (var quantum in accountQuanta)
            {
                var effects = new Dictionary<byte[], Effect>();
                foreach (var rawEffect in quantum.Effects)
                {
                    var effect = XdrConverter.Deserialize<Effect>(rawEffect);
                    effects.Add(rawEffect.ComputeHash(), effect.Account == account ? effect : null);
                }
                accountEffects.Add(new ApexEffects
                {
                    Apex = quantum.Apex,
                    Items = effects.Values.ToList(),
                    Proof = new EffectsProof
                    {
                        Hashes = effects.Keys.Select(e => new Hash { Data = e }).ToList(),
                        Signatures = quantum.Signatures.Select(s => new Ed25519Signature { Signature = s }).ToList()
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

        public void ApplyUpdates(List<IPersistentModel> updates)
        {
            lock (syncRoot)
                Context.PermanentStorage.SaveBatch(updates);
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
        /// Creates snapshot
        /// </summary>
        /// <returns></returns>
        public static Snapshot GetSnapshot(ulong apex, ConstellationSettings settings, List<AccountWrapper> accounts, List<OrderWrapper> orders, Dictionary<string, string> cursors, byte[] quantumHash)
        {
            if (apex < 1)
                throw new ArgumentException("Apex must be greater than zero.");

            var snapshot = new Snapshot
            {
                Apex = apex,
                Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts)),
                Orders = orders ?? throw new ArgumentNullException(nameof(orders)),
                Settings = settings ?? throw new ArgumentNullException(nameof(settings)),
                Cursors = cursors ?? throw new ArgumentNullException(nameof(cursors)),
                LastHash = quantumHash ?? throw new ArgumentNullException(nameof(quantumHash))
            };
            return snapshot;
        }

        /// <summary>
        /// Fetches settings for the specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public ConstellationSettings GetConstellationSettings()
        {
            var settingsModel = Context.PermanentStorage.LoadSettings(ulong.MaxValue);
            if (settingsModel == null)
                return null;

            return settingsModel.ToDomainModel();
        }

        //TODO: move it to separate class
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

            var settings = GetConstellationSettings();
            if (settings == null)
                return null;

            var cursors = Context.PermanentStorage.LoadCursors();

            var accounts = (GetAccounts(settings.GetBaseAsset())).Select(a => new AccountWrapper(a, settings.RequestRateLimits));

            var accountStorage = new AccountStorage(accounts);

            var orders = accounts.SelectMany(a => a.Account.Orders.Select(o => new OrderWrapper(o, a))).OrderBy(o => o.Order.OrderId).ToList();
            var exchange = GetRestoredExchange(orders, settings);

            var quanta = Context.PermanentStorage.LoadQuantaAboveApex(apex);

            var effects = quanta.SelectMany(q => q.Effects.Select(e =>
            {
                var effect = XdrConverter.Deserialize<Effect>(e);
                effect.Apex = q.Apex;
                return effect;
            })
            ).ToList();

            for (var i = effects.Count - 1; i >= 0; i--)
            {
                var currentEffect = effects[i];

                var account = currentEffect.Account > 0 ? accountStorage.GetAccount(currentEffect.Account) : null;
                IEffectProcessor<Effect> processor = null;
                switch (currentEffect)
                {
                    case ConstellationUpdateEffect constellationUpdateEffect:
                        settings = constellationUpdateEffect.PrevSettings;
                        break;
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

        private Exchange GetRestoredExchange(List<OrderWrapper> orders, ConstellationSettings settings)
        {
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
