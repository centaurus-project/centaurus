using Centaurus.Domain.Quanta.Sync;
using Centaurus.Models;
using Centaurus.PersistentStorage;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class SyncStorage : ContextualBase
    {
        private MemoryCache quantaDataCache = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });

        static Logger logger = LogManager.GetCurrentClassLogger();
        public SyncStorage(ExecutionContext context, ulong lastApex)
            : base(context)
        {
            context.PendingUpdatesManager.OnBatchSaved += PendingUpdatesManager_OnBatchSaved;

            PortionSize = Context.Settings.SyncBatchSize;

            var batchId = GetBatchApexStart(lastApex);
            //load current quanta batch
            GetBatch(batchId);
            //run cache cleanup worker
            CacheCleanupWorker();
        }

        public const int BatchSize = 1_000_000;

        public int PortionSize { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from">Exclusive</param>
        /// <param name="force">if force, than batch would be return even if it's not fulfilled yet</param>
        /// <returns></returns>
        public SyncPortion GetQuanta(ulong from, bool force)
        {
            var quantaBatchStart = GetBatchApexStart(from + 1);
            var batch = GetBatch(quantaBatchStart);
        
            return batch.Quanta.GetData(from, force);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from">Exclusive</param>
        /// <param name="force">if force, than batch would be return even if it's not fulfilled yet</param>
        /// <returns></returns>
        public SyncPortion GetSignatures(ulong from, bool force)
        {
            var quantaBatchStart = GetBatchApexStart(from + 1);
            var batch = GetBatch(quantaBatchStart);

            return batch.Signatures.GetData(from, force);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from">Exclusive</param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public List<SyncQuantaBatchItem> GetQuanta(ulong from, int limit)
        {
            var quantaBatchStart = GetBatchApexStart(from + 1);
            var batch = GetBatch(quantaBatchStart);

            return batch.Quanta.GetItems(from, limit);
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
            var batch = GetBatch(quantaBatchStart);

            return batch.Signatures.GetItems(from, limit);
        }

        public void AddQuantum(ulong apex, SyncQuantaBatchItem quantum)
        {
            var batchStart = GetBatchApexStart(apex);
            var batch = GetBatch(batchStart);
            batch.Quanta.Add(apex, quantum);
        }

        public void AddSignatures(ulong apex, QuantumSignatures signatures)
        {
            var batchStart = GetBatchApexStart(apex);
            var batch = GetBatch(batchStart);
            batch.Signatures.Add(apex, signatures);
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
                            if (MarkBatchAsFulfilled(batchId))
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

        private bool MarkBatchAsFulfilled(ulong batchId)
        {
            if (!quantaDataCache.TryGetValue<SyncStorageItem>(batchId, out var batch) || !batch.IsFulfilled)
            {
                logger.Info("Batch is not found or not full yet");
                return false;
            }

            var options = new MemoryCacheEntryOptions();
            options.SetSlidingExpiration(TimeSpan.FromSeconds(15));
            options.RegisterPostEvictionCallback(OnPostEviction);

            //replace old entry
            quantaDataCache.Set(batchId, batch, options);
            return true;
        }

        private object quantaSyncRoot = new { };

        private SyncStorageItem GetBatch(ulong batchId)
        {
            var batch = default(SyncStorageItem);
            if (!quantaDataCache.TryGetValue(batchId, out batch))
                lock (quantaSyncRoot)
                {
                    if (!quantaDataCache.TryGetValue(batchId, out batch))
                    {
                        var options = new MemoryCacheEntryOptions();
                        batch = LoadBatch(batchId, BatchSize);
                        if (!batch.IsFulfilled)
                            options.Priority = CacheItemPriority.NeverRemove;
                        else
                            options.SetSlidingExpiration(TimeSpan.FromSeconds(15));
                        options.RegisterPostEvictionCallback(OnPostEviction);

                        quantaDataCache.Set(batchId, batch, options);
                    }
                }
            return batch;
        }

        private ulong GetBatchApexStart(ulong apex)
        {
            return apex - (apex % BatchSize);
        }

        private void OnPostEviction(object key, object value, EvictionReason reason, object state)
        {
            var batch = (SyncStorageItem)value;
            logger.Info($"Batch {key} of quantum data with range from {batch.BatchStart} to {batch.BatchEnd} is removed because of {reason}. State: {state}.");
        }

        private void PendingUpdatesManager_OnBatchSaved(BatchSavedInfo batchSavedInfo)
        {
            QuantaRangePersisted(batchSavedInfo.FromApex, batchSavedInfo.ToApex);
        }

        private SyncStorageItem LoadBatch(ulong batchStartApex, int batchSize)
        {
            logger.Info($"About to load quanta batch from db. Start apex: {batchStartApex}, size: {batchSize}.");
            var limit = batchSize;
            var aboveApex = batchStartApex;
            if (batchStartApex == 0)
                //first quantum has 1 apex, and null will be set at 0 index. So we need to load batch size - 1
                limit = batchSize - 1;
            else
                aboveApex = batchStartApex - 1;

            var rawQuanta = (IEnumerable<QuantumPersistentModel>)Context.PersistentStorage.LoadQuantaAboveApex(aboveApex, batchSize)
                .OrderBy(q => q.Apex);

            logger.Info($"Quanta batch with apex start {batchStartApex} is loaded.");

            var quanta = new List<SyncQuantaBatchItem>();
            var signatures = new List<QuantumSignatures>();
            foreach (var rawQuantum in rawQuanta)
            {
                quanta.Add(rawQuantum.ToBatchItemQuantum());
                signatures.Add(rawQuantum.ToQuantumSignatures());
            }

            if (batchStartApex == 0) //insert null at 0 position, otherwise index will not be relevant to apex
            {
                quanta.Insert(0, null);
                signatures.Insert(0, null);
            }

            return new SyncStorageItem(Context, batchStartApex, PortionSize, quanta, signatures);
        }

        class SyncStorageItem: ContextualBase
        {
            public SyncStorageItem(ExecutionContext context, ulong batchId, int portionSize, List<SyncQuantaBatchItem> quanta, List<QuantumSignatures> signatures)
                :base(context)
            {
                BatchStart = batchId;
                BatchEnd = batchId + BatchSize - 1; //batch start is inclusive
                Quanta = new ApexItemsBatch<SyncQuantaBatchItem>(Context, batchId, BatchSize, portionSize, quanta);
                Signatures = new ApexItemsBatch<QuantumSignatures>(Context, batchId, BatchSize, portionSize, signatures);
            }

            public ApexItemsBatch<QuantumSignatures> Signatures { get; }

            public ApexItemsBatch<SyncQuantaBatchItem> Quanta { get; }

            public bool IsFulfilled => Quanta.IsFulfilled && Signatures.IsFulfilled;

            public ulong BatchStart { get; }

            public ulong BatchEnd { get; }
        }
    }
}
