using Centaurus.Domain.Models;
using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Centaurus.Domain
{
    public class DataProvider : ContextualBase
    {
        private object syncRoot = new { };

        public DataProvider(ExecutionContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Loads account's quantum info data.
        /// </summary>
        /// <param name="cursor">Query cursor.</param>
        /// <param name="isDesc">Is descending direction.</param>
        /// <param name="limit">Maximum items in batch.</param>
        /// <param name="account">Current account</param>
        /// <returns></returns>
        public QuantumInfoResponse LoadQuantaInfo(string cursor, bool isDesc, int limit, RawPubKey account)
        {
            if (account == null)
                throw new ArgumentException("Account must be specified.");

            ulong.TryParse(cursor, out var apex);
            var order = isDesc ? QueryOrder.Desc : QueryOrder.Asc;
            var accountQuanta = Context.PersistentStorage.LoadQuantaForAccount(account, apex, limit, order);
            var accountQuantumInfos = new List<QuantumInfo>();

            foreach (var accountQuantum in accountQuanta)
            {
                var effects = new List<EffectsInfoBase>();

                var quantum = accountQuantum.Quantum;

                foreach (var effectsGroup in quantum.Effects)
                {
                    //send effects group object if the account is initiator, otherwise send hash
                    if (effectsGroup.Account.Equals(account))
                        effects.Add(new EffectsInfo { EffectsGroupData = effectsGroup.Effects });
                    else
                        effects.Add(new EffectsHashInfo { EffectsGroupData = effectsGroup.Effects.ComputeHash() });
                }

                accountQuantumInfos.Add(new QuantumInfo
                {
                    Apex = accountQuantum.Quantum.Apex,
                    Items = effects,
                    Proof = quantum.Signatures.Select(s => new TinySignature
                    {
                        Data = s.PayloadSignature
                    }).ToList(),
                    Request = accountQuantum.IsInitiator
                        //send full quantum if the account is initiator, otherwise send hash
                        ? (RequestInfoBase)new RequestInfo { Data = quantum.RawQuantum }
                        : new RequestHashInfo { Data = quantum.RawQuantum.ComputeHash() }
                });
            }

            //reverse results for desc request
            if (isDesc)
            {
                accountQuantumInfos.Reverse();
                accountQuantumInfos.ForEach(ae => ae.Items.Reverse());
            }
            return new QuantumInfoResponse
            {
                CurrentPagingToken = cursor,
                Order = isDesc ? QuantumInfoRequest.Desc : QuantumInfoRequest.Asc,
                Items = accountQuantumInfos,
                NextPageToken = accountQuantumInfos.LastOrDefault()?.Apex.ToString(),
                PrevPageToken = accountQuantumInfos.FirstOrDefault()?.Apex.ToString(),
                Limit = limit
            };
        }

        /// <summary>
        /// Fetches all quanta where apex is greater than the specified one.
        /// </summary>
        /// <param name="apex"></param>
        /// <param name="count">Count of quanta to load. Loads all if equal or less than 0</param>
        /// <returns></returns>
        public List<SyncQuantaBatchItem> GetQuantaSyncBatchItemsAboveApex(ulong apex, int count = 0)
        {
            var query = (IEnumerable<QuantumPersistentModel>)Context.PersistentStorage.LoadQuantaAboveApex(apex, count)
                .OrderBy(q => q.Apex);
            if (count > 0)
                query = query.Take(count);
            return query.Select(q => q.ToBatchItemQuantum()).ToList();
        }

        /// <summary>
        /// Fetches all quanta's signatures where apex is greater than the specified one.
        /// </summary>
        /// <param name="apex"></param>
        /// <param name="count">Count of quanta to load. Loads all if equal or less than 0</param>
        /// <returns></returns>
        public List<QuantumSignatures> GetSignaturesSyncBatchItemsAboveApex(ulong apex, int count = 0)
        {
            var query = (IEnumerable<QuantumPersistentModel>)Context.PersistentStorage.LoadQuantaAboveApex(apex, count)
                .OrderBy(q => q.Apex);
            return query.Select(q => q.ToQuantumSignatures()).ToList();
        }

        /// <summary>
        /// Get last persisted apex
        /// </summary>
        /// <returns></returns>
        public ulong GetLastApex()
        {
            return Context.PersistentStorage.GetLastApex();
        }

        public (Snapshot snapshot, List<PendingQuantum> pendingQuanta) GetPersistentData()
        {
            (Snapshot snapshot, List<PendingQuantum> pendingQuanta) data = default;
            var lastApex = GetLastApex();
            if (lastApex > 0)
                data.snapshot = GetSnapshot(lastApex);
            data.pendingQuanta = LoadPendingQuanta();
            return data;
        }

        /// <summary>
        /// Builds snapshot for specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        public Snapshot GetSnapshot(ulong apex)
        {
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

            var cursors = Context.PersistentStorage.LoadCursors()?.Cursors ?? new Dictionary<string, string>();

            var accounts = GetAccounts(settings.QuoteAsset.Code, settings.RequestRateLimits);

            var accountStorage = new AccountStorage(accounts);

            var orders = accounts.SelectMany(a => a.Orders.Values.Select(o => new OrderWrapper(o, a)))
                .OrderBy(o => o.Order.OrderId)
                .ToList();

            var exchange = GetRestoredExchange(orders, settings);

            var batchSize = 1_000_000;
            var quanta = new List<QuantumPersistentModel>();
            while (true)
            {
                var items = Context.PersistentStorage.LoadQuantaAboveApex(apex, batchSize).ToList();
                if (items.Count > 0)
                {
                    quanta.AddRange(items);
                    if (items.Count == batchSize)
                        continue;
                }
                break;
            }

            var effects = quanta
                .SelectMany(q =>
                {
                    var effects = new List<Effect>();
                    foreach (var rawEffectsGroup in q.Effects)
                    {
                        var effectsGroup = XdrConverter.Deserialize<EffectsGroup>(rawEffectsGroup.Effects);
                        foreach (var effect in effectsGroup.Effects)
                        {
                            effect.Apex = q.Apex;
                            if (effect is AccountEffect accountEffect)
                                accountEffect.Account = effectsGroup.Account;
                            effects.Add(effect);
                        }
                    }
                    return effects;
                }).ToList();

            for (var i = effects.Count - 1; i >= 0; i--)
            {
                var currentEffect = effects[i];

                var account = currentEffect is AccountEffect accountEffect
                    ? accountStorage.GetAccount(accountEffect.Account)
                    : null;
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
                        processor = new BalanceUpdateEffectProcesor(balanceUpdateEffect, account);
                        break;
                    case RequestRateLimitUpdateEffect requestRateLimitUpdateEffect:
                        processor = new RequestRateLimitUpdateEffectProcessor(requestRateLimitUpdateEffect, account, settings.RequestRateLimits);
                        break;
                    case OrderPlacedEffect orderPlacedEffect:
                        {
                            var orderBook = exchange.GetOrderbook(orderPlacedEffect.Asset, orderPlacedEffect.Side);
                            var order = exchange.OrderMap.GetOrder(orderPlacedEffect.Apex);
                            processor = new OrderPlacedEffectProcessor(orderPlacedEffect, order.Account, orderBook, order, settings.QuoteAsset.Code);
                        }
                        break;
                    case OrderRemovedEffect orderRemovedEffect:
                        {
                            var orderBook = exchange.GetOrderbook(orderRemovedEffect.Asset, orderRemovedEffect.Side);
                            processor = new OrderRemovedEffectProccessor(orderRemovedEffect, accountStorage.GetAccount(orderRemovedEffect.Account), orderBook, settings.QuoteAsset.Code);
                        }
                        break;
                    case TradeEffect tradeEffect:
                        {
                            var order = exchange.OrderMap.GetOrder(tradeEffect.Apex);
                            if (order == null) //no need to revert trade if no order was created
                                continue;
                            processor = new TradeEffectProcessor(tradeEffect, order.Account, order, settings.QuoteAsset.Code);
                        }
                        break;
                    case CursorUpdateEffect cursorUpdateEffect:
                        {
                            if (cursorUpdateEffect.PrevCursor == null)
                                cursors.Remove(cursorUpdateEffect.Provider);
                            else
                                cursors[cursorUpdateEffect.Provider] = cursorUpdateEffect.PrevCursor;
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }

                processor.RevertEffect();
            }

            var lastQuantumData = (Quantum)XdrConverter.Deserialize<Message>(Context.PersistentStorage.LoadQuantum(apex).RawQuantum);

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
                Accounts = accountStorage.GetAll().OrderBy(a => a.Pubkey.ToString()).ToList(),
                Orders = allOrders.OrderBy(o => o.OrderId).ToList(),
                Settings = settings,
                LastHash = lastQuantumData.ComputeHash(),
                Cursors = cursors
            };
        }

        public void SaveBatch(List<IPersistentModel> updates)
        {
            lock (syncRoot)
                Context.PersistentStorage.SaveBatch(updates);
        }

        public List<PendingQuantum> LoadPendingQuanta()
        {
            var pendingQuantaModel = Context.PersistentStorage.LoadPendingQuanta();
            if (pendingQuantaModel == null)
                return null;
            return pendingQuantaModel.Quanta.Select(q => q.ToDomainModel()).ToList();
        }

        /// <summary>
        /// Fetches settings for the specified apex
        /// </summary>
        /// <param name="apex"></param>
        /// <returns></returns>
        private ConstellationSettings GetConstellationSettings()
        {
            var settingsModel = Context.PersistentStorage.LoadSettings(ulong.MaxValue);
            if (settingsModel == null)
                return null;

            return settingsModel.ToDomainModel();
        }

        /// <summary>
        /// Returns minimal apex a snapshot can be reverted to
        /// </summary>
        /// <returns></returns>
        private ulong GetMinRevertApex()
        {
            //obtain min apex we can revert to
            var minApex = Context.PersistentStorage.LoadQuanta().FirstOrDefault()?.Apex ?? 0;
            if (minApex == 0) //we can't revert at all
                return 0;

            return minApex - 1; //we can revert effect for that apex, so the minimal apex is first effect apex - 1
        }

        private Exchange GetRestoredExchange(List<OrderWrapper> orders, ConstellationSettings settings)
        {
            return Exchange.RestoreExchange(settings.Assets, orders, false);
        }

        private List<Account> GetAccounts(string baseAsset, RequestRateLimits requestRateLimits)
        {
            return Context.PersistentStorage.LoadAccounts()
                .Select(a => a.ToDomainModel(baseAsset, requestRateLimits))
                .ToList();
        }
    }
}
