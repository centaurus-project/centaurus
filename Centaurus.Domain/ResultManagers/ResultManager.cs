using Centaurus.Models;
using Centaurus.PaymentProvider.Models;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Centaurus.Domain.ProcessorContext;

namespace Centaurus.Domain
{
    public class ResultManager : ContextualBase, IDisposable
    {
        public ResultManager(ExecutionContext context)
            : base(context)
        {
            InitTimers();
            currentPubKey = Context.Settings.KeyPair;
        }

        public void Add(QuantaProcessingResult processingResult)
        {
            var aggregate = default(Aggregate);
            lock (pendingAggregatesSyncRoot)
                if (!pendingAggregates.TryGetValue(processingResult.Apex, out aggregate))
                {
                    pendingAggregates.Add(processingResult.Apex, new Aggregate(processingResult, this));
                    return;
                }
            aggregate.Add(processingResult);
            aggregate.Item.ProcessOutrunSignatures(aggregate);
        }

        public void Add(AuditorResult resultMessage)
        {
            var aggregate = default(Aggregate);
            lock (pendingAggregatesSyncRoot)
            {
                //result message could be received before quantum was processed
                if (!pendingAggregates.TryGetValue(resultMessage.Apex, out aggregate))
                {
                    aggregate = new Aggregate(resultMessage, this);
                    //if result apex is less than or equal to last processed apex, then the result is no more relevant
                    if (resultMessage.Apex > Context.QuantumStorage.CurrentApex)
                        pendingAggregates.Add(resultMessage.Apex, aggregate);
                    return;
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
                    logger.Trace($"Unable to remove item by id '{id}'");
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
            var now = DateTime.UtcNow;
            var itemsToRemove = default(List<ulong>);
            lock (pendingAggregatesSyncRoot)
            {
                itemsToRemove = pendingAggregates
                    .Where(kv => now - kv.Value.CreatedAt > aggregateLifeTime)
                    .Select(kv => kv.Key)
                    .ToList();
            }

            foreach (var itemKey in itemsToRemove)
                Remove(itemKey);
            cleanupTimer?.Start();
        }

        private System.Timers.Timer cleanupTimer;
        private System.Timers.Timer acknowledgmentTimer;

        protected static Logger logger = LogManager.GetCurrentClassLogger();

        private object pendingAggregatesSyncRoot = new { };
        private Dictionary<ulong, Aggregate> pendingAggregates = new Dictionary<ulong, Aggregate>();
        private readonly RawPubKey currentPubKey;

        class Aggregate
        {
            private Aggregate(ResultManager manager)
            {
                Manager = manager;
                CreatedAt = DateTime.UtcNow;
            }

            public Aggregate(QuantaProcessingResult processingResult, ResultManager manager)
                : this(manager)
            {
                Item = new AggregateItem(processingResult, this);
                IsAcknowledgmentSent = Item.Result.Initiator == 0 && Item.Result.Effects.All(eg => eg.Account == 0);
                Item.ProcessOutrunSignatures(this);
            }

            public Aggregate(AuditorResult resultMessage, ResultManager resultManager)
                : this(resultManager)
            {
                Item = new AggregateItem(resultMessage);
            }

            public bool IsProcessed { get; private set; }

            public bool IsAcknowledgmentSent { get; private set; }

            public DateTime CreatedAt { get; }

            public AggregateItem Item { get; }

            public ResultManager Manager { get; }

            private object syncRoot = new { };
            private HashSet<int> processedAuditors = new HashSet<int>();
            private List<AuditorResult> resultMessages = new List<AuditorResult>();

            public void Add(AuditorResult resultMessage)
            {
                lock (syncRoot)
                {
                    //skip if processed or current auditor already sent the result
                    if (IsProcessed || processedAuditors.Contains(resultMessage.Signature.AuditorId))
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

                    //TODO: add extra validations. Auditors can send invalid results
                    processedAuditors.Add(resultMessage.Signature.AuditorId);

                    //check if signature is valid and tx signature is presented for TxResultMessage
                    if (resultMessage.Signature.PayloadSignature.IsValid(auditor, Item.Result.PayloadHash))
                    {
                        resultMessages.Add(resultMessage);

                        //skip if current node signature, it already added
                        if (!auditor.Equals(Manager.currentPubKey))
                            //add signatures to cached quantum
                            Manager.Context.QuantumStorage.AddResult(resultMessage);
                    }

                    var majorityResult = CheckMajority();
                    if (majorityResult == MajorityResults.Unknown)
                        return;

                    OnResult(majorityResult);
                    Manager.Remove(Item.Apex);
                    IsProcessed = true;
                }
            }

            public void Add(QuantaProcessingResult processingResult)
            {
                lock (syncRoot)
                {
                    Item.AssignResult(processingResult, this);
                }
            }

            public void SendResult(bool isAcknowledgment = false)
            {
                lock (syncRoot)
                {
                    if (IsProcessed || (isAcknowledgment && IsAcknowledgmentSent))
                        return;

                    var effectsProof = new PayloadProof
                    {
                        PayloadHash = Item.Result.PayloadHash,
                        Signatures = resultMessages.Select(r => r.Signature.PayloadSignature).ToList()
                    };

                    foreach (var notification in Item.Result.GetNotificationMessages(effectsProof))
                    {
                        var aPubKey = Item.Result.AffectedAccounts[notification.Key];
                        Manager.Context.Notify(aPubKey, notification.Value.CreateEnvelope<MessageEnvelopeSigneless>());
                    }

                    if (Item.Result.Initiator != 0)
                    {
                        var aPubKey = Item.Result.AffectedAccounts[Item.Result.Initiator];
                        Item.Result.ResultMessage.PayloadProof = effectsProof;
                        Item.Result.ResultMessage.Effects = Item.Result.Effects.GetAccountEffects(Item.Result.Initiator);
                        Manager.Context.Notify(aPubKey, Item.Result.ResultMessage.CreateEnvelope<MessageEnvelopeSigneless>());
                    }
                    IsAcknowledgmentSent = true;
                }
            }

            private void SubmitTransaction()
            {
                try
                {
                    if (Item.Result.ResultMessage.Quantum is WithdrawalRequestQuantum transactionQuantum)
                    {
                        if (!Manager.Context.PaymentProvidersManager.TryGetManager(transactionQuantum.ProviderId, out var paymentProvider))
                            throw new Exception($"Unable to find manager {transactionQuantum.ProviderId}");
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

                    var originalEnvelope = Item.Result.ResultMessage.OriginalMessage;
                    SequentialRequestMessage requestMessage = null;
                    if (originalEnvelope.Message is SequentialRequestMessage)
                        requestMessage = (SequentialRequestMessage)originalEnvelope.Message;
                    else if (originalEnvelope.Message is RequestQuantum)
                        requestMessage = ((RequestQuantum)originalEnvelope.Message).RequestEnvelope.Message as SequentialRequestMessage;

                    var exc = new Exception("Majority for quantum" +
                        $" {Item.Apex} ({requestMessage?.GetMessageType() ?? originalEnvelope.Message.GetMessageType()})" +
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
                lock (syncRoot)
                {
                    if (IsProcessed)
                        return;

                    Manager.Context.PendingUpdatesManager.AddSignatures(Item.Result.UpdatesBatchId, Item.Apex, resultMessages);
                }
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
            public AggregateItem(AuditorResult result)
            {
                Apex = result.Apex;
                AddResultMessage(result);
            }

            public AggregateItem(QuantaProcessingResult processingResult, Aggregate aggregate)
            {
                AssignResult(processingResult, aggregate);
                Apex = processingResult.Apex;
            }

            public void AssignResult(QuantaProcessingResult result, Aggregate aggregate)
            {
                Result = result ?? throw new ArgumentNullException(nameof(result));

                //add current node signature to head
                outrunResults.Insert(0, new AuditorResult
                {
                    Apex = Result.Apex,
                    Signature = Result.CurrentNodeSignature
                });
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
