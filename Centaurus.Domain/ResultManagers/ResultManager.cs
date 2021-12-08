﻿using Centaurus.Models;
using Centaurus.PaymentProvider.Models;
using Centaurus.Xdr;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class ResultManager : ContextualBase, IDisposable
    {

        private MemoryCache resultCache = new MemoryCache(new MemoryCacheOptions());

        const int batchSize = 1_000_000;

        public ResultManager(ExecutionContext context)
            : base(context)
        {
            //create batch for current apex
            CreateBatch(GetBatchApexStart(Context.QuantumHandler.CurrentApex));
            //update batch if required
            UpdateCache();

            InitTimers();
            Task.Factory.StartNew(ProcessResults, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(ProcessAuditorResults, TaskCreationOptions.LongRunning);
        }

        public void Add(QuantumProcessingItem processingResult)
        {
            results.Add(processingResult);
        }

        public void Add(QuantumSignatures resultMessage)
        {
            auditorResults.Add(resultMessage);
        }

        public void CompleteAdding()
        {
            results.CompleteAdding();
            auditorResults.CompleteAdding();
        }

        public bool TryGetResult(ulong apex, out QuantumProcessingItem processingItem)
        {
            processingItem = null;
            var batchId = GetBatchApexStart(apex);
            if (!resultCache.TryGetValue(batchId, out List<Aggregate> batch))
                return false;
            var index = (int)(apex - batchId);
            processingItem = batch[index].ProcessingItem;
            return true;
        }

        public bool IsCompleted => results.IsCompleted && auditorResults.IsCompleted;

        private void OnPostEviction(object key, object value, EvictionReason reason, object state)
        {
            logger.Info($"Batch {key} with {((List<Aggregate>)value).Count} is removed because of {reason}. State: {state}.");
        }

        private ulong GetBatchApexStart(ulong apex)
        {
            return apex - (apex % batchSize);
        }

        private BlockingCollection<QuantumProcessingItem> results = new BlockingCollection<QuantumProcessingItem>();

        private BlockingCollection<QuantumSignatures> auditorResults = new BlockingCollection<QuantumSignatures>();

        private void ProcessResults()
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 5 };
            var partitioner = Partitioner.Create(results.GetConsumingEnumerable(), EnumerablePartitionerOptions.NoBuffering);
            Parallel.ForEach(partitioner, options, result =>
            {
                try
                {
                    AddInternal(result);
                }
                catch (Exception exc)
                {
                    Context.NodesManager.CurrentNode.Failed(new Exception($"Error on processing result for apex {result.Apex}", exc));
                    return;
                }
            });
        }

        private void ProcessAuditorResults()
        {

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 5 };
            var partitioner = Partitioner.Create(auditorResults.GetConsumingEnumerable(), EnumerablePartitionerOptions.NoBuffering);
            Parallel.ForEach(partitioner, options, result =>
            {
                try
                {
                    AddInternal(result);
                }
                catch (Exception exc)
                {
                    Context.NodesManager.CurrentNode.Failed(new Exception($"Error on auditor result for apex {result.Apex}", exc));
                    return;
                }
            });
        }

        private void AddInternal(QuantumSignatures signaturesMessage)
        {
            //auditor can send delayed results
            if (signaturesMessage.Apex <= Context.PendingUpdatesManager.LastPersistedApex)
                return;
            //add the signature to the aggregate
            var aggregate = GetAggregate(signaturesMessage.Apex);
            foreach (var signature in signaturesMessage.Signatures)
                aggregate.Add(signature);
        }

        private Aggregate GetAggregate(ulong apex)
        {
            if (!TryGetAggregate(apex, out var aggregate))
                throw new Exception($"Unable to find aggregate for apex {apex}.");
            return aggregate;
        }

        private bool TryGetAggregate(ulong apex, out Aggregate aggregate)
        {
            aggregate = null;
            var batchId = GetBatchApexStart(apex);
            if (!resultCache.TryGetValue(batchId, out Lazy<Aggregate>[] batch))
                return false;
            var index = (int)(apex - batchId);
            aggregate = batch[index].Value;
            return true;
        }

        private void AddInternal(QuantumProcessingItem processingResult)
        {
            GetAggregate(processingResult.Apex).Add(processingResult);
        }

        public bool TryGetSignatures(ulong apex, out List<NodeSignatureInternal> signatures)
        {
            TryGetAggregate(apex, out var aggregate);
            signatures = aggregate?.GetSignatures();
            return signatures != null && signatures.Count > 0;
        }

        public virtual void Dispose()
        {
            cacheUpdateTimer?.Stop();
            cacheUpdateTimer?.Dispose();
            cacheUpdateTimer = null;
        }

        private void InitTimers()
        {
            cacheUpdateTimer = new System.Timers.Timer();
            cacheUpdateTimer.Interval = 5 * 1000;
            cacheUpdateTimer.AutoReset = false;
            cacheUpdateTimer.Elapsed += (s, e) => UpdateCache();
            cacheUpdateTimer.Start();
        }

        private ulong advanceThreshold = 500_000;

        private List<ulong> currentBatchIds = new List<ulong>();

        private void UpdateCache()
        {
            try
            {
                var currentBatchId = currentBatchIds.Last();
                var nextBatchId = currentBatchId + batchSize;
                if (nextBatchId - Context.QuantumHandler.LastAddedQuantumApex < advanceThreshold)
                    CreateBatch(nextBatchId);
                //copy batch to be able to modify original
                var _currentBatchIds = currentBatchIds.ToList();
                foreach (var batchId in _currentBatchIds)
                {
                    if (batchId == currentBatchId || batchId + batchSize > Context.PendingUpdatesManager.LastPersistedApex)
                        break;
                    currentBatchIds.Remove(batchId);
                    if (!resultCache.TryGetValue(batchId, out var batch))
                    {
                        Context.NodesManager.CurrentNode.Failed(new Exception($"Result batch {batchId} is not found."));
                        return;
                    }
                    var memoryCacheOption = new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromSeconds(15) };
                    memoryCacheOption.RegisterPostEvictionCallback(OnPostEviction);
                    //replace old entry
                    resultCache.Set(batchId, batch, memoryCacheOption);
                }
                cacheUpdateTimer?.Start();
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Error on results cleanup.");
            }
        }

        private void CreateBatch(ulong batchId)
        {
            logger.Info($"About to generate new result items batch {batchId}.");
            var batch = new Lazy<Aggregate>[batchSize];
            Parallel.For(0, batchSize, i =>
            {
                var apex = batchId + (ulong)i;
                batch[i] = new Lazy<Aggregate>(() => new Aggregate(apex, this));
            });
            var memoryCacheOption = new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove };
            memoryCacheOption.RegisterPostEvictionCallback(OnPostEviction);
            resultCache.Set(batchId, batch, memoryCacheOption);
            currentBatchIds.Add(batchId);
            logger.Info($"Batch {batchId} generated and set.");
        }

        private System.Timers.Timer cacheUpdateTimer;

        protected static Logger logger = LogManager.GetCurrentClassLogger();

        class Aggregate
        {
            public Aggregate(ulong apex, ResultManager manager)
            {
                Apex = apex;
                Manager = manager;
            }

            public ulong Apex { get; }

            public ResultManager Manager { get; }

            public QuantumProcessingItem ProcessingItem { get; private set; }

            private bool IsFinalized;
            private object syncRoot = new { };
            private HashSet<int> processedAuditors = new HashSet<int>();
            private List<NodeSignatureInternal> signatures = new List<NodeSignatureInternal>();
            private List<NodeSignatureInternal> outrunResults = new List<NodeSignatureInternal>();

            public List<NodeSignatureInternal> GetSignatures()
            {
                lock (syncRoot)
                    return signatures.ToList();
            }

            public void Add(NodeSignatureInternal signature)
            {
                var majorityResult = MajorityResult.Unknown;
                lock (syncRoot)
                {

                    if (processedAuditors.Contains(signature.AuditorId))
                        return;

                    //if current server is not Alpha than it can delay
                    if (ProcessingItem == null)
                    {
                        outrunResults.Add(signature);
                        return;
                    }

                    //obtain auditor from constellation
                    if (!Manager.Context.AuditorIds.TryGetValue((byte)signature.AuditorId, out var auditor))
                        return;

                    //check if signature is valid and tx signature is presented for TxResultMessage
                    if (signature.PayloadSignature.IsValid(auditor, ProcessingItem.PayloadHash))
                        AddValidSignature(signature);

                    majorityResult = GetMajorityResult();
                    if (IsFinalized || majorityResult == MajorityResult.Unknown || !IsSignedByAlpha())
                        return;
                    else if (majorityResult == MajorityResult.Unreachable)
                    {
                        var votesCount = signatures.Count;

                        var quantum = ProcessingItem.Quantum;
                        SequentialRequestMessage requestMessage = quantum is RequestQuantumBase requestQuantum
                            ? requestQuantum.RequestMessage
                            : null;

                        var exc = new Exception("Majority for quantum" +
                            $" {Apex} ({requestMessage?.GetMessageType() ?? quantum.GetMessageType()})" +
                            $" is unreachable. Results received count is {processedAuditors.Count}," +
                            $" valid results count is {votesCount}. The constellation collapsed.");
                        ProcessingItem.SetException(exc);
                        Manager.Context.NodesManager.CurrentNode.Failed(exc);
                    }

                    IsFinalized = true;
                }
                OnResult();
            }

            public void Add(QuantumProcessingItem quantumProcessingItem)
            {
                lock (syncRoot)
                {
                    ProcessingItem = quantumProcessingItem;
                    var signature = AddCurrentNodeSignature();
                    //mark as acknowledged
                    ProcessingItem.Acknowledged();
                    //send result to auditors
                    Manager.Context.NodesManager.EnqueueResult(new AuditorResult { Apex = Apex, Signature = signature });
                    ProcessOutrunSignatures(this);
                }
            }

            private void AddValidSignature(NodeSignatureInternal signature)
            {
                processedAuditors.Add(signature.AuditorId);
                //skip if processed or current auditor already sent the result
                if (IsFinalized)
                    return;
                //Alpha signature must always be first
                if (signature.AuditorId == ProcessingItem.AlphaId)
                {
                    signatures.Insert(0, signature);
                    ProcessingItem.ResultMessage.PayloadProof.Signatures.Insert(0, signature.PayloadSignature);
                    //add quantum to quantum sync storage
                    Manager.Context.SyncStorage.AddQuantum(Apex,
                        new SyncQuantaBatchItem
                        {
                            Quantum = ProcessingItem.Quantum,
                            AlphaSignature = signature
                        });
                }
                else
                {
                    signatures.Add(signature);
                    ProcessingItem.ResultMessage.PayloadProof.Signatures.Add(signature.PayloadSignature);
                }
            }

            private bool IsSignedByAlpha()
            {
                return signatures.Any(s => s.AuditorId == ProcessingItem.AlphaId);
            }

            private NodeSignatureInternal AddCurrentNodeSignature()
            {
                //get current node signature
                var signature = GetSignature();
                //add to results without validation
                AddValidSignature(signature);
                return signature;
            }

            private NodeSignatureInternal GetSignature()
            {
                var currentAuditorSignature = new NodeSignatureInternal
                {
                    AuditorId = ProcessingItem.CurrentAuditorId,
                    PayloadSignature = ProcessingItem.PayloadHash.Sign(Manager.Context.Settings.KeyPair)
                };

                //add transaction signature
                if (ProcessingItem.Quantum is WithdrawalRequestQuantum withdrawal)
                {
                    var provider = Manager.Context.PaymentProvidersManager.GetManager(withdrawal.Provider);
                    var signature = provider.SignTransaction(withdrawal.Transaction);
                    currentAuditorSignature.TxSignature = signature.Signature;
                    currentAuditorSignature.TxSigner = signature.Signer;
                }
                return currentAuditorSignature;
            }

            private void ProcessOutrunSignatures(Aggregate aggregate)
            {
                foreach (var result in outrunResults)
                    aggregate.Add(result);
            }

            private void SendResult()
            {
                var notifications = ProcessingItem.GetNotificationMessages();
                foreach (var notification in notifications)
                    Manager.Context.Notify(notification.Key, notification.Value.CreateEnvelope<MessageEnvelopeSignless>());

                if (ProcessingItem.Initiator != null)
                    Manager.Context.Notify(ProcessingItem.Initiator.Pubkey, ProcessingItem.ResultMessage.CreateEnvelope<MessageEnvelopeSignless>());
            }

            private void SubmitTransaction()
            {
                try
                {
                    if (ProcessingItem.Quantum is WithdrawalRequestQuantum transactionQuantum)
                    {
                        if (!Manager.Context.PaymentProvidersManager.TryGetManager(transactionQuantum.Provider, out var paymentProvider))
                            throw new Exception($"Unable to find manager {transactionQuantum.Provider}");
                        var txSignatures = signatures
                            .Where(s => s.TxSignature != null)
                            .Select(s => new SignatureModel { Signature = s.TxSignature, Signer = s.TxSigner })
                            .ToList();
                        paymentProvider.SubmitTransaction(transactionQuantum.Transaction, txSignatures);
                    }
                }
                catch (Exception exc)
                {
                    logger.Error(exc, "Error on transaction submit");
                }
            }

            private void OnResult()
            {
                SubmitTransaction();
                PersistSignatures();
                //mark as finalized
                ProcessingItem.Finalized();
                SendResult();
            }

            private void PersistSignatures()
            {
                //assign signatures to persistent model
                ProcessingItem.PersistentModel.Signatures = signatures.Select(s => s.ToPersistenModel()).ToList();
                //add signatures to sync storage
                Manager.Context.SyncStorage.AddSignatures(Apex, new QuantumSignatures
                {
                    Apex = Apex,
                    Signatures = signatures.Skip(1).ToList() //skip first. The first signature will be synced with quantum
                });
            }

            private MajorityResult GetMajorityResult()
            {
                int requiredMajority = Manager.Context.GetMajorityCount(),
                    maxVotes = Manager.Context.GetTotalAuditorsCount();

                var votesCount = signatures.Count;

                //check if we have the majority
                if (votesCount >= requiredMajority)
                    return MajorityResult.Success;

                var totalOpposition = processedAuditors.Count - votesCount;
                var maxPossibleVotes = votesCount + (maxVotes - totalOpposition);
                if (maxPossibleVotes < requiredMajority)
                    return MajorityResult.Unreachable;//no chances to reach the majority

                //not enough votes to decided whether the consensus can be reached or not
                return MajorityResult.Unknown;
            }
        }
    }
}
