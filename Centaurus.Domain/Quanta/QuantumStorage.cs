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
        private MemoryCache quantaCache = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromSeconds(15), SizeLimit = 2_000_000 });

        static Logger logger = LogManager.GetCurrentClassLogger();
        public QuantumStorage(ExecutionContext context)
            : base(context)
        {
        }

        const int batchSize = 500_000;


        public ulong CurrentApex { get; private set; }
        public byte[] LastQuantumHash { get; private set; }

        public void Init(ulong lastApex, byte[] lastQuantumHash)
        {
            CurrentApex = lastApex;
            LastQuantumHash = lastQuantumHash;
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

        private List<PendingQuantum> GetBatch(ulong batchId)
        {
            var batch = default(List<PendingQuantum>);
            if (!quantaCache.TryGetValue(batchId, out batch))
                lock (quantaCache)
                {
                    if (!quantaCache.TryGetValue(batchId, out batch))
                    {
                        batch = LoadQuantaFromDB(batchId);
                        if (batchId == 0) //insert null at 0 position, otherwise index will not be relevant to apex
                            batch.Insert(0, null);
                        if (batch.Count != batchSize) //set capacity for pending batch
                            batch.Capacity = batchSize;
                        quantaCache.Set(batchId, batch, new MemoryCacheEntryOptions { Size = batchSize });
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
            var quantaModels = Context.PermanentStorage.LoadQuantaAboveApex(aboveApex); //LoadQuantaAboveApex returns exclusive results
            var query = quantaModels
                .OrderBy(q => q.Apex)
                .Take(batchSize);
            logger.Info($"Batch with apex start {batchStartApex} is loaded.");
            return query.Select(q => q.ToPendingQuantum()).ToList();
        }
    }
}
