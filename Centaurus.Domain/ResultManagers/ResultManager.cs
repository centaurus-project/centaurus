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
        }

        private void ProcessResults()
        {
            foreach (var resultItem in results.GetConsumingEnumerable())
            {
                try
                {
                    if (resultItem is QuantaProcessingResult result)
                        AddInternal(result);
                    else
                        AddInternal((AuditorResult)resultItem);
                }
                catch (Exception exc)
                {
                    logger.Error(exc, "Error on processing result");
                }
            }
        }

        private BlockingCollection<object> results = new BlockingCollection<object>();
        public void Add(QuantaProcessingResult processingResult)
        {
            results.Add(processingResult);
        }

        private void AddInternal(QuantaProcessingResult processingResult)
        {
            var aggregate = default(Aggregate);
            lock (pendingAggregatesSyncRoot)
                if (!pendingAggregates.TryGetValue(processingResult.Apex, out aggregate))
                {
                    aggregate = new Aggregate(processingResult.Apex, this);
                    pendingAggregates.Add(processingResult.Apex, aggregate);
                }
            aggregate.Add(processingResult);
        }

        public void Add(AuditorResult resultMessage)
        {
            results.Add(resultMessage);
        }

        private void AddInternal(AuditorResult resultMessage)
        {
            var aggregate = default(Aggregate);
            lock (pendingAggregatesSyncRoot)
            {
                //result message could be received before quantum was processed
                if (!pendingAggregates.TryGetValue(resultMessage.Apex, out aggregate))
                {
                    //auditor can send delayed results
                    if (resultMessage.Apex <= Context.PendingUpdatesManager.LastSavedApex)
                        return;

                    aggregate = new Aggregate(resultMessage.Apex, this);
                    pendingAggregates.Add(resultMessage.Apex, aggregate);
                }
            }

            //add the signature to the aggregate
            aggregate.Add(resultMessage);
        }

        /// <summary>
        /// Remove an aggregate by message id.
        /// </summary>
        /// <param name="messageId">Message id key.</param>
        public void Remove(ulong id)
        {
            lock (pendingAggregatesSyncRoot)
            {
                if (!pendingAggregates.Remove(id, out _))
                {
                    Console.WriteLine($"Unable to remove item by id '{id}'");
                    logger.Trace($"Unable to remove item by id '{id}'");
                }
            }
        }

        public QuantumResultMessageBase GetResult(ulong id)
        {
            lock (pendingAggregatesSyncRoot)
            {
                if (pendingAggregates.TryGetValue(id, out var aggregate))
                    return aggregate.Item.Result.ResultMessage;
                return null;
            }
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
            cleanupTimer.Interval = 30 * 1000;
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
            var acknowledgmentTimeout = TimeSpan.FromMilliseconds(100);
            var resultsToSend = default(List<Aggregate>);
            lock (pendingAggregatesSyncRoot)
            {
                resultsToSend = pendingAggregates.Values
                    .Where(a => a.Item.IsResultAssigned && !a.IsAcknowledgmentSent && DateTime.UtcNow - a.CreatedAt > acknowledgmentTimeout)
                    .ToList();
            }
            foreach (var resultItem in resultsToSend)
                resultItem.SendResult(true);
            acknowledgmentTimer.Start();
        }

        private TimeSpan aggregateLifeTime = new TimeSpan(0, 1, 0);

        private void Cleanup()
        {
            try
            {
                var now = DateTime.UtcNow;
                var itemsToRemove = new List<ulong>();
                lock (pendingAggregatesSyncRoot)
                {
                    if (pendingAggregates.Count < 1_000_000)
                        return;
                    foreach (var aggregate in pendingAggregates)
                    {
                        if (now - aggregate.Value.CreatedAt > aggregateLifeTime || aggregate.Key < Context.PendingUpdatesManager.LastSavedApex)
                        {
                            itemsToRemove.Add(aggregate.Key);
                        }
                        else
                            break;
                    }
                }

                foreach (var itemKey in itemsToRemove)
                    Remove(itemKey);
                cleanupTimer?.Start();
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc);
            }
        }

        private System.Timers.Timer cleanupTimer;
        private System.Timers.Timer acknowledgmentTimer;

        protected static Logger logger = LogManager.GetCurrentClassLogger();

        private object pendingAggregatesSyncRoot = new { };
        private SortedDictionary<ulong, Aggregate> pendingAggregates = new SortedDictionary<ulong, Aggregate>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from">From cursor (exclusive)</param>
        /// <param name="limit">Max items to load</param>
        /// <returns></returns>
        public bool TryGetResults(ulong from, int limit, out List<QuantumSignatures> auditorResults)
        {
            auditorResults = null;
            lock (pendingAggregatesSyncRoot)
            {
                var firstApex = pendingAggregates.FirstOrDefault().Key;
                if (firstApex == 0 || firstApex > (from + 1))
                    return false;
                var skip = (int)(from - firstApex) + 1;
                if (skip >= pendingAggregates.Count)
                    return false;
                auditorResults = pendingAggregates
                    .Skip(skip)
                    .Take(limit)
                    .Select(a =>
                    {
                        return new QuantumSignatures
                        {
                            Apex = a.Key,
                            Signatures = a.Value.GetResults().Select(s => s.Signature).ToList()
                        };
                    }).ToList();
            }
            return true;
        }

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
                        {
                            //all signatures received
                            if (processedAuditors.Count == Manager.Context.AuditorIds.Count)
                                Manager.Remove(Item.Apex);
                            return;
                        }
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
                Manager.Context.PendingUpdatesManager.AddSignatures(Item.Result.UpdatesBatchId, Item.Apex, resultMessages);
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
