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

namespace Centaurus.Domain
{
    public class QuantumStorage : ContextualBase
    {
        private MemoryCache quantaCache = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });

        static Logger logger = LogManager.GetCurrentClassLogger();
        public QuantumStorage(ExecutionContext context)
            : base(context)
        {
            context.PendingUpdatesManager.OnBatchSaved += PendingUpdatesManager_OnBatchSaved;
        }

        const int batchSize = 1_000_000;


        public ulong CurrentApex { get; private set; }
        public byte[] LastQuantumHash { get; private set; }

        public void Init(ulong lastApex, byte[] lastQuantumHash)
        {
            CurrentApex = lastApex;
            LastQuantumHash = lastQuantumHash;
            //load current batch
            GetBatch(GetBatchApexStart(CurrentApex));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from">Exclusive</param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public List<PendingQuantum> GetQuanta(ulong from, int limit)
        {
            from = from + 1;
            var quantaBatchStart = GetBatchApexStart(from);
            var batch = GetBatch(quantaBatchStart);

            var skip = (int)(from - quantaBatchStart);
            return batch.Skip(skip).Take(limit).ToList();
        }

        public void AddQuantum(Quantum quantum, byte[] quantumHash)
        {
            var batchStart = GetBatchApexStart(quantum.Apex);
            var batch = GetBatch(batchStart);
            batch.Add(new PendingQuantum { Quantum = quantum });
            LastQuantumHash = quantumHash;
            CurrentApex = quantum.Apex;
        }

        public void AddSignatures(ulong apex, List<AuditorSignatureInternal> auditorSignatures)
        {
            var batchStart = GetBatchApexStart(apex);
            var batch = GetBatch(batchStart);

            var currentQuantumIndex = (int)(apex - batchStart);
            if (currentQuantumIndex >= batch.Count)
                throw new Exception($"Quantum {apex} is not added yet.");

            batch[currentQuantumIndex].Signatures = auditorSignatures;
        }

        private void QuantaRangePersisted(ulong from, ulong to)
        {
            var fromBatchId = GetBatchApexStart(from);
            var toBatchId = GetBatchApexStart(to + 1);
            if (fromBatchId == toBatchId) //if the same batch start, than there is some quanta to persist
                return;

            if (!quantaCache.TryGetValue<List<PendingQuantum>>(fromBatchId, out var batch) || batch.Count != batchSize)
                logger.Error("Batch is not found or not full yet");

            var options = new MemoryCacheEntryOptions();
            options.SetSlidingExpiration(TimeSpan.FromSeconds(15));
            options.RegisterPostEvictionCallback(OnPostEviction);

            //replace old entry
            quantaCache.Set(fromBatchId, batch, options);
        }

        private object syncRoot = new { };

        private List<PendingQuantum> GetBatch(ulong batchId)
        {
            var batch = default(List<PendingQuantum>);
            if (!quantaCache.TryGetValue(batchId, out batch))
                lock (syncRoot)
                {
                    if (!quantaCache.TryGetValue(batchId, out batch))
                    {
                        var options = new MemoryCacheEntryOptions();
                        batch = LoadQuantaFromDB(batchId);
                        if (batchId == 0) //insert null at 0 position, otherwise index will not be relevant to apex
                            batch.Insert(0, null);
                        batch.Capacity = batchSize;
                        if (batch.Count != batchSize)
                            options.Priority = CacheItemPriority.NeverRemove;
                        else
                            options.SetSlidingExpiration(TimeSpan.FromSeconds(15));
                        options.RegisterPostEvictionCallback(OnPostEviction);

                        quantaCache.Set(batchId, batch, options);
                    }
                }
            return batch;
        }

        private ulong GetBatchApexStart(ulong apex)
        {
            return apex - (apex % batchSize);
        }

        private List<PendingQuantum> LoadQuantaFromDB(ulong batchStartApex)
        {
            logger.Info($"About to load quanta batch from db. Start apex: {batchStartApex}, size: {batchSize}.");
            var aboveApex = batchStartApex > 0 ? batchStartApex - 1 : 0;
            var quantaModels = Context.PersistentStorage.LoadQuantaAboveApex(aboveApex); //LoadQuantaAboveApex returns exclusive results
            var query = quantaModels
                .OrderBy(q => q.Apex)
                .Take(batchSize);
            logger.Info($"Batch with apex start {batchStartApex} is loaded.");
            return query.Select(q => q.ToPendingQuantum()).ToList();
        }

        private void OnPostEviction(object key, object value, EvictionReason reason, object state)
        {
            logger.Info($"Batch {key} with {((List<PendingQuantum>)value).Count} is removed because of {reason}. State: {state}.");
        }

        private void PendingUpdatesManager_OnBatchSaved(BatchSavedInfo batchSavedInfo)
        {
            QuantaRangePersisted(batchSavedInfo.FromApex, batchSavedInfo.ToApex);
        }
    }
}
