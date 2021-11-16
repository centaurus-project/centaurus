using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.PersistentStorage.Abstraction;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class SyncStorage : ContextualBase
    {
        private MemoryCache quantaCache = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });
        private MemoryCache signaturesCache = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });

        static Logger logger = LogManager.GetCurrentClassLogger();
        public SyncStorage(ExecutionContext context, ulong lastApex)
            : base(context)
        {
            context.PendingUpdatesManager.OnBatchSaved += PendingUpdatesManager_OnBatchSaved;
            //LastAddedQuantumApex = LastAddedSignaturesApex = lastApex;
            var batchId = GetBatchApexStart(lastApex);
            //load current quanta batch
            GetQuantaBatch(batchId);
            //load current signatures batch
            GetSignaturesBatch(batchId);
            //run cache cleanup worker
            CacheCleanupWorker();
        }

        const int batchSize = 1_000_000;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from">Exclusive</param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public List<SyncQuantaBatchItem> GetQuanta(ulong from, int limit)
        {
            var quantaBatchStart = GetBatchApexStart(from + 1);
            var batch = GetQuantaBatch(quantaBatchStart);

            return batch.GetItems(from, limit);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from">Exclusive</param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public List<QuantumSignatures> GetSignatures(ulong from, int limit)
        {
            var quantaBatchStart = GetBatchApexStart(from + 1);
            var batch = GetSignaturesBatch(quantaBatchStart);

            return batch.GetItems(from, limit);
        }

        public void AddQuantum(ulong apex, SyncQuantaBatchItem quantum)
        {
            var batchStart = GetBatchApexStart(apex);
            var batch = GetQuantaBatch(batchStart);
            batch.Add(apex, quantum);
        }

        public void AddSignatures(ulong apex, QuantumSignatures signatures)
        {
            var batchStart = GetBatchApexStart(apex);
            var batch = GetSignaturesBatch(batchStart);
            batch.Add(apex, signatures);
        }

        private object persistedBatchIdsSyncRoot = new { };
        private List<ulong> persistedBatchIds = new List<ulong>();
        private void QuantaRangePersisted(ulong from, ulong to)
        {
            var fromBatchId = GetBatchApexStart(from);
            var toBatchId = GetBatchApexStart(to + 1);
            if (fromBatchId == toBatchId) //if the same batch start, than there is some quanta to persist
                return;

            lock (persistedBatchIdsSyncRoot)
                if (!persistedBatchIds.Contains(fromBatchId))
                    persistedBatchIds.Add(fromBatchId);
        }

        private void CacheCleanupWorker()
        {
            Task.Factory.StartNew(() =>
            {
                var hasItemToRemove = true;
                while (true)
                {
                    if (!hasItemToRemove)
                        Thread.Sleep(3000);

                    lock (persistedBatchIdsSyncRoot)
                        if (persistedBatchIds.Count > 0)
                        {
                            var batchId = persistedBatchIds[0];
                            if (MarkBatchAsFulfilled<QuantumSignatures>(signaturesCache, batchId) && MarkBatchAsFulfilled<SyncQuantaBatchItem>(quantaCache, batchId))
                            {
                                persistedBatchIds.RemoveAt(0);
                                hasItemToRemove = true;
                                continue;
                            }
                        }
                    hasItemToRemove = false;
                }
            });
        }

        private bool MarkBatchAsFulfilled<T>(MemoryCache cache, ulong batchId)
            where T : IApex
        {
            if (!cache.TryGetValue<ApexItemsBatch<T>>(batchId, out var batch) || batch.LastApex != (batchId + batchSize - 1))
            {
                logger.Info("Batch is not found or not full yet");
                return false;
            }

            var options = new MemoryCacheEntryOptions();
            options.SetSlidingExpiration(TimeSpan.FromSeconds(15));
            options.RegisterPostEvictionCallback(OnPostEviction<T>);

            //replace old entry
            cache.Set(batchId, batch, options);
            return true;
        }

        private object quantaSyncRoot = new { };

        private ApexItemsBatch<SyncQuantaBatchItem> GetQuantaBatch(ulong batchId)
        {
            var batch = default(ApexItemsBatch<SyncQuantaBatchItem>);
            if (!quantaCache.TryGetValue(batchId, out batch))
                lock (quantaSyncRoot)
                {
                    if (!quantaCache.TryGetValue(batchId, out batch))
                    {
                        var options = new MemoryCacheEntryOptions();
                        var items = LoadQuantaFromDB(batchId);
                        if (batchId == 0) //insert null at 0 position, otherwise index will not be relevant to apex
                            items.Insert(0, null);
                        batch = new ApexItemsBatch<SyncQuantaBatchItem>(batchId, batchSize, items);
                        if (items.Count != batchSize)
                            options.Priority = CacheItemPriority.NeverRemove;
                        else
                            options.SetSlidingExpiration(TimeSpan.FromSeconds(15));
                        options.RegisterPostEvictionCallback(OnPostEviction<SyncQuantaBatchItem>);

                        quantaCache.Set(batchId, batch, options);
                    }
                }
            return batch;
        }

        object signaturesSyncRoot = new { };
        private ApexItemsBatch<QuantumSignatures> GetSignaturesBatch(ulong batchId)
        {
            var batch = default(ApexItemsBatch<QuantumSignatures>);
            if (!signaturesCache.TryGetValue(batchId, out batch))
                lock (signaturesSyncRoot)
                {
                    if (!signaturesCache.TryGetValue(batchId, out batch))
                    {
                        var options = new MemoryCacheEntryOptions();
                        var items = LoadSignaturesFromDB(batchId);
                        if (batchId == 0) //insert null at 0 position, otherwise index will not be relevant to apex
                            items.Insert(0, null);
                        batch = new ApexItemsBatch<QuantumSignatures>(batchId, batchSize, items);
                        if (items.Count != batchSize)
                            options.Priority = CacheItemPriority.NeverRemove;
                        else
                            options.SetSlidingExpiration(TimeSpan.FromSeconds(15));
                        options.RegisterPostEvictionCallback(OnPostEviction<QuantumSignatures>);

                        signaturesCache.Set(batchId, batch, options);
                    }
                }
            return batch;
        }

        private ulong GetBatchApexStart(ulong apex)
        {
            return apex - (apex % batchSize);
        }

        private List<SyncQuantaBatchItem> LoadQuantaFromDB(ulong batchStartApex)
        {
            logger.Info($"About to load quanta batch from db. Start apex: {batchStartApex}, size: {batchSize}.");
            var limit = batchSize;
            var aboveApex = batchStartApex;
            if (batchStartApex == 0)
                //first quantum has 1 apex, and null will be set at 0 index. So we need to load batch size - 1
                limit = batchSize - 1;
            else
                aboveApex = batchStartApex - 1;
            var quanta = Context.DataProvider.GetQuantaSyncBatchItemsAboveApex(aboveApex, limit);
            logger.Info($"Quanta batch with apex start {batchStartApex} is loaded.");
            return quanta;
        }

        private List<QuantumSignatures> LoadSignaturesFromDB(ulong batchStartApex)
        {
            logger.Info($"About to load quanta batch from db. Start apex: {batchStartApex}, size: {batchSize}.");
            var limit = batchSize;
            var aboveApex = batchStartApex;
            if (batchStartApex == 0)
                //first quantum has 1 apex, and null will be set at 0 index. So we need to load batch size - 1
                limit = batchSize - 1;
            else
                aboveApex = batchStartApex - 1;
            var quanta = Context.DataProvider.GetSignaturesSyncBatchItemsAboveApex(aboveApex, limit);
            logger.Info($"Signatures batch with apex start {batchStartApex} is loaded.");
            return quanta;
        }

        private void OnPostEviction<T>(object key, object value, EvictionReason reason, object state)
            where T : IApex
        {
            var batch = (ApexItemsBatch<T>)value;
            logger.Info($"Batch {key} of {typeof(T).Name} with range from {batch.Start} to {batch.LastApex} is removed because of {reason}. State: {state}.");
        }

        private void PendingUpdatesManager_OnBatchSaved(BatchSavedInfo batchSavedInfo)
        {
            QuantaRangePersisted(batchSavedInfo.FromApex, batchSavedInfo.ToApex);
        }
    }
}
