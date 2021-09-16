using Centaurus.Models;
using Centaurus.PaymentProvider.Models;
using Centaurus.Xdr;
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
        public ResultManager(ExecutionContext context)
            : base(context)
        {
            InitTimers();
            Task.Factory.StartNew(ProcessResults, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(ProcessAuditorResults, TaskCreationOptions.LongRunning);
        }

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
                    logger.Error(exc, "Error on processing result");
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
                    logger.Error(exc, "Error on processing auditor result");
                }
            });
        }


        private BlockingCollection<QuantaProcessingResult> results = new BlockingCollection<QuantaProcessingResult>();
        public void Add(QuantaProcessingResult processingResult)
        {
            results.Add(processingResult);
        }

        private void AddInternal(QuantaProcessingResult processingResult)
        {
            GetAggregate(processingResult.Apex).Add(processingResult);
        }

        private BlockingCollection<AuditorResult> auditorResults = new BlockingCollection<AuditorResult>();
        public void Add(AuditorResult resultMessage)
        {
            auditorResults.Add(resultMessage);
        }

        public void AddInternal(AuditorResult resultMessage)
        {
            //auditor can send delayed results
            if (resultMessage.Apex <= Context.PendingUpdatesManager.LastSavedApex)
                return;
            //add the signature to the aggregate
            GetAggregate(resultMessage.Apex).Add(resultMessage);
        }

        private Aggregate GetAggregate(ulong apex)
        {
            if (!pendingAggregates.TryGetValue(apex, out var aggregate))
                lock (pendingAggregatesSyncRoot)
                    if (!pendingAggregates.TryGetValue(apex, out aggregate))
                    {
                        aggregate = new Aggregate(apex, this);
                        pendingAggregates.Add(apex, aggregate);
                    }
            return aggregate;
        }

        /// <summary>
        /// Remove an aggregate by message id.
        /// </summary>
        /// <param name="messageId">Message id key.</param>
        public void Remove(ulong id)
        {
            lock (pendingAggregatesSyncRoot)
                if (!pendingAggregates.Remove(id, out _))
                {
                    logger.Info($"Unable to remove item by id '{id}'");
                }
        }

        public QuantumResultMessageBase GetResult(ulong id)
        {
            return GetAggregate(id)?.Item?.Result.ResultMessage;
        }

        public virtual void Dispose()
        {
            cleanupTimer?.Stop();
            cleanupTimer?.Dispose();
            cleanupTimer = null;

            acknowledgmentTimer?.Stop();
            acknowledgmentTimer?.Dispose();
            acknowledgmentTimer = null;
        }

        private void InitTimers()
        {
            cleanupTimer = new System.Timers.Timer();
            cleanupTimer.Interval = 5 * 1000;
            cleanupTimer.AutoReset = false;
            cleanupTimer.Elapsed += (s, e) => Cleanup();
            cleanupTimer.Start();

            acknowledgmentTimer = new System.Timers.Timer();
            acknowledgmentTimer.Interval = 200;
            acknowledgmentTimer.AutoReset = false;
            acknowledgmentTimer.Elapsed += (s, e) => SendAcknowledgment();
            acknowledgmentTimer.Start();
        }

        private void SendAcknowledgment()
        {
            //var acknowledgmentTimeout = TimeSpan.FromMilliseconds(100);
            //var resultsToSend = default(List<Aggregate>);
            //lock (pendingAggregatesSyncRoot)
            //{
            //    resultsToSend = pendingAggregates.Values
            //        .Where(a => a.Item.IsResultAssigned && !a.IsAcknowledgmentSent && DateTime.UtcNow - a.CreatedAt > acknowledgmentTimeout)
            //        .ToList();
            //}
            //foreach (var resultItem in resultsToSend)
            //    resultItem.SendResult(true);
            //acknowledgmentTimer.Start();
        }

        private ulong advanceThreshold = 50_000;
        private void Cleanup()
        {
            try
            {
                if (Context.PendingUpdatesManager.LastSavedApex > advanceThreshold)
                {
                    var apexRemoveLimit = Context.PendingUpdatesManager.LastSavedApex - advanceThreshold;
                    var apexToRemove = 0ul;
                    lock (pendingAggregatesSyncRoot)
                        apexToRemove = pendingAggregates.FirstOrDefault().Key;
                    while (apexToRemove != 0 && apexToRemove < apexRemoveLimit)
                    {
                        Remove(apexToRemove);
                        apexToRemove++;
                    }
                }
                cleanupTimer?.Start();
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Error on results cleanup.");
            }
        }

        private System.Timers.Timer cleanupTimer;
        private System.Timers.Timer acknowledgmentTimer;

        protected static Logger logger = LogManager.GetCurrentClassLogger();

        private object pendingAggregatesSyncRoot = new { };
        private SortedDictionary<ulong, Aggregate> pendingAggregates = new SortedDictionary<ulong, Aggregate>();

        class Aggregate
        {
            public Aggregate(ulong apex, ResultManager manager)
            {
                Item = new AggregateItem(apex);
                Manager = manager;
                CreatedAt = DateTime.UtcNow;
            }

            public bool IsProcessed { get; private set; }

            public bool IsAcknowledgmentSent { get; private set; }

            public DateTime CreatedAt { get; }

            public AggregateItem Item { get; }

            public ResultManager Manager { get; }

            private object syncRoot = new { };
            private HashSet<int> processedAuditors = new HashSet<int>();
            private List<AuditorResult> resultMessages = new List<AuditorResult>();

            public List<AuditorResult> GetResults()
            {
                return resultMessages.ToList();
            }

            public void Add(AuditorResult resultMessage)
            {
                var majorityResult = MajorityResults.Unknown;
                lock (syncRoot)
                {

                    if (processedAuditors.Contains(resultMessage.Signature.AuditorId))
                        return;

                    //if current server is not Alpha than it can delay
                    if (!Item.IsResultAssigned)
                    {
                        Item.AddResultMessage(resultMessage);
                        return;
                    }


                    //obtain auditor from constellation
                    if (!Manager.Context.AuditorIds.TryGetValue((byte)resultMessage.Signature.AuditorId, out var auditor))
                        return;

                    //check if signature is valid and tx signature is presented for TxResultMessage
                    if (resultMessage.Signature.PayloadSignature.IsValid(auditor, Item.Result.PayloadHash))
                    {
                        processedAuditors.Add(resultMessage.Signature.AuditorId);
                        //skip if processed or current auditor already sent the result
                        if (IsProcessed)
                            return;
                        resultMessages.Add(resultMessage);
                    }

                    majorityResult = CheckMajority();
                    if (majorityResult == MajorityResults.Unknown)
                    {
                        return;
                    }
                    IsProcessed = true;
                }
                OnResult(majorityResult);
            }

            private AuditorResult AddCurrentNodeSignature()
            {
                //get current node signature
                var signature = GetSignature();
                var result = new AuditorResult { Apex = Item.Apex, Signature = signature };
                //add to results without validation
                resultMessages.Add(new AuditorResult { Apex = Item.Apex, Signature = signature });
                processedAuditors.Add(signature.AuditorId);
                return result;
            }


            private AuditorSignatureInternal GetSignature()
            {
                var currentAuditorSignature = new AuditorSignatureInternal
                {
                    AuditorId = Item.Result.CurrentAuditorId,
                    PayloadSignature = Item.Result.PayloadHash.Sign(Manager.Context.Settings.KeyPair)
                };

                //add transaction signature
                if (Item.Result.Quantum is WithdrawalRequestQuantum withdrawal)
                {
                    var provider = Manager.Context.PaymentProvidersManager.GetManager(withdrawal.Provider);
                    var signature = provider.SignTransaction(withdrawal.Transaction);
                    currentAuditorSignature.TxSignature = signature.Signature;
                    currentAuditorSignature.TxSigner = signature.Signer;
                }
                return currentAuditorSignature;
            }

            public void Add(QuantaProcessingResult processingResult)
            {
                lock (syncRoot)
                {
                    Item.AssignResult(processingResult);
                    var result = AddCurrentNodeSignature();
                    //send result to auditors
                    Manager.Context.OutgoingConnectionManager.EnqueueResult(result);
                    Item.ProcessOutrunSignatures(this);
                }
            }



            public void SendResult(bool isAcknowledgment = false)
            {
                lock (syncRoot)
                {
                    if (isAcknowledgment && (IsProcessed || IsAcknowledgmentSent))
                        return;
                    IsAcknowledgmentSent = true;
                }

                var effectsProof = new PayloadProof
                {
                    PayloadHash = Item.Result.PayloadHash,
                    Signatures = resultMessages.Select(r => r.Signature.PayloadSignature).ToList()
                };

                if (IsProcessed) //send notification only after majority received
                {
                    var notifications = Item.Result.GetNotificationMessages(Item.Result.Initiator, new RequestHashInfo { Data = Item.Result.QuantumHash }, effectsProof);
                    foreach (var notification in notifications)
                    {
                        Manager.Context.Notify(notification.Key, notification.Value.CreateEnvelope<MessageEnvelopeSignless>());
                    }
                }

                if (Item.Result.Initiator != null)
                {
                    Item.Result.ResultMessage.PayloadProof = effectsProof;
                    Item.Result.ResultMessage.Effects = Item.Result.Effects.GetAccountEffects(Item.Result.Initiator);
                    Manager.Context.Notify(Item.Result.Initiator, Item.Result.ResultMessage.CreateEnvelope<MessageEnvelopeSignless>());
                }
            }

            private void SubmitTransaction()
            {
                try
                {
                    if (Item.Result.Quantum is WithdrawalRequestQuantum transactionQuantum)
                    {
                        if (!Manager.Context.PaymentProvidersManager.TryGetManager(transactionQuantum.Provider, out var paymentProvider))
                            throw new Exception($"Unable to find manager {transactionQuantum.Provider}");
                        var signatures = resultMessages
                            .Where(r => r.Signature.TxSignature != null)
                            .Select(r => new SignatureModel { Signature = r.Signature.TxSignature, Signer = r.Signature.TxSigner })
                            .ToList();
                        paymentProvider.SubmitTransaction(transactionQuantum.Transaction, signatures);
                    }
                }
                catch (Exception exc)
                {
                    logger.Error(exc, "Error on transaction submit");
                }
            }

            private void OnResult(MajorityResults majorityResult)
            {
                if (majorityResult == MajorityResults.Unreachable)
                {
                    var votesCount = resultMessages.Count;

                    var quantum = Item.Result.Quantum;
                    SequentialRequestMessage requestMessage = quantum is RequestQuantumBase requestQuantum
                        ? requestQuantum.RequestMessage
                        : null;

                    var exc = new Exception("Majority for quantum" +
                        $" {Item.Apex} ({requestMessage?.GetMessageType() ?? quantum.GetMessageType()})" +
                        $" is unreachable. Results received count is {processedAuditors.Count}," +
                        $" valid results count is {votesCount}. The constellation collapsed.");
                    Manager.Context.StateManager.Failed(exc);
                    throw exc;
                }
                SubmitTransaction();
                SendResult();
                PersistSignatures();
            }

            private void PersistSignatures()
            {
                Manager.Context.QuantumStorage.AddSignatures(Item.Apex, resultMessages.Select(r => r.Signature).ToList());
                Item.Result.UpdatesBatch.AddSignatures(Item.Apex, resultMessages);
            }

            private MajorityResults CheckMajority()
            {
                int requiredMajority = Manager.Context.GetMajorityCount(),
                    maxVotes = Manager.Context.GetTotalAuditorsCount();

                var votesCount = resultMessages.Count;

                //check if we have the majority
                if (votesCount >= requiredMajority)
                    return MajorityResults.Success;

                var totalOpposition = processedAuditors.Count - votesCount;
                var maxPossibleVotes = votesCount + (maxVotes - totalOpposition);
                if (maxPossibleVotes < requiredMajority)
                    return MajorityResults.Unreachable;//no chances to reach the majority

                //not enough votes to decided whether the consensus can be reached or not
                return MajorityResults.Unknown;
            }
        }

        class AggregateItem
        {
            public AggregateItem(ulong apex)
            {
                Apex = apex;
            }

            public void AssignResult(QuantaProcessingResult result)
            {
                Result = result ?? throw new ArgumentNullException(nameof(result));
            }

            public void AddResultMessage(AuditorResult result)
            {
                outrunResults.Add(result);
            }

            public ulong Apex { get; }

            public QuantaProcessingResult Result { get; private set; }

            public bool IsResultAssigned => Result != null;

            List<AuditorResult> outrunResults { get; } = new List<AuditorResult>();

            public void ProcessOutrunSignatures(Aggregate aggregate)
            {
                foreach (var result in outrunResults)
                    aggregate.Add(result);
            }
        }
    }
}
