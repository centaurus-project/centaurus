using Centaurus.Models;
using Centaurus.PaymentProvider.Models;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        }

        public void Add(uint updatesBatchId, ulong apex, QuantumResultMessageBase resultMessage, Dictionary<ulong, Message> notifications)
        {
            var aggregate = default(ResultConsensusAggregate);
            lock (pendingAggregatesSyncRoot)
            {
                if (!pendingAggregates.TryGetValue(apex, out aggregate))
                {
                    pendingAggregates.Add(apex, new ResultConsensusAggregate(updatesBatchId, resultMessage, notifications, this));
                    return;
                }
            }

            aggregate.Add(updatesBatchId, resultMessage, notifications);
        }

        public void Add(AuditorResultMessage resultMessage, RawPubKey auditor)
        {
            var aggregate = default(ResultConsensusAggregate);
            lock (pendingAggregatesSyncRoot)
            {
                if (!pendingAggregates.TryGetValue(resultMessage.Apex, out aggregate))
                {
                    //if result apex is less than or equal to last processed apex, then the result is no more relevant
                    if (resultMessage.Apex > Context.QuantumStorage.CurrentApex)
                        pendingAggregates.Add(resultMessage.Apex, new ResultConsensusAggregate(resultMessage, auditor, this));
                    return;
                }
            }

            //add the signature to the aggregate
            aggregate.Add(resultMessage, auditor);
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
            var resultsToSend = default(List<ResultConsensusAggregate>);
            lock (pendingAggregatesSyncRoot)
            {
                resultsToSend = pendingAggregates.Values
                    .Where(a => a.ResultMessageItem.IsResultAssigned && !a.IsAcknowledgmentSent && DateTime.UtcNow - a.CreatedAt > acknowledgmentTimeout)
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
        private Dictionary<ulong, ResultConsensusAggregate> pendingAggregates = new Dictionary<ulong, ResultConsensusAggregate>();

        class ResultConsensusAggregate
        {
            private ResultConsensusAggregate(ResultManager resultManager)
            {
                ResultManager = resultManager;
                CreatedAt = DateTime.UtcNow;
            }

            public ResultConsensusAggregate(uint updatesBatchId, QuantumResultMessageBase result, Dictionary<ulong, Message> notifications, ResultManager resultManager)
                : this(resultManager)
            {
                ResultMessageItem = new ResultMessageItem(updatesBatchId, result, notifications, this);
                IsAcknowledgmentSent = ResultMessageItem.AccountPubKey == null && ResultMessageItem.Notifications.Count < 1;
            }

            public ResultConsensusAggregate(AuditorResultMessage resultMessage, RawPubKey auditor, ResultManager resultManager)
                : this(resultManager)
            {
                ResultMessageItem = new ResultMessageItem(resultMessage, auditor);
            }

            public bool IsProcessed { get; private set; }
            public bool IsAcknowledgmentSent { get; private set; }

            public DateTime CreatedAt { get; }
            public ResultMessageItem ResultMessageItem { get; }
            public ResultManager ResultManager { get; }

            private object syncRoot = new { };
            private HashSet<RawPubKey> processedAuditors = new HashSet<RawPubKey>();
            private List<AuditorResultMessage> resultMessages = new List<AuditorResultMessage>();

            public void Add(AuditorResultMessage resultMessage, RawPubKey auditor)
            {
                lock (syncRoot)
                {
                    //skip if processed or current auditor already sent the result
                    if (IsProcessed || processedAuditors.Contains(auditor))
                        return;

                    //if current server is not Alpha than it can delay
                    if (!ResultMessageItem.IsResultAssigned)
                    {
                        ResultMessageItem.AddResultMessage(resultMessage, auditor);
                        return;
                    }

                    //add current auditor to processed
                    processedAuditors.Add(auditor);

                    //check if signature is valid and tx signature is presented for TxResultMessage
                    if (resultMessage.Signature.EffectsSignature.IsValid(auditor, ResultMessageItem.EffectsHash))
                    {
                        resultMessages.Add(resultMessage);

                        //add signatures to result
                        ResultMessageItem.Result.Effects.Signatures.Add(resultMessage.Signature.EffectsSignature);

                        //add signatures to cached quantum
                        ResultManager.Context.QuantumStorage.AddResult(resultMessage);
                    }

                    var majorityResult = CheckMajority();
                    if (majorityResult == MajorityResults.Unknown)
                        return;

                    OnResult(majorityResult);
                    ResultManager.Remove(ResultMessageItem.Apex);
                    IsProcessed = true;
                }
            }

            public void Add(uint updatesBatchId, QuantumResultMessageBase result, Dictionary<ulong, Message> notifications)
            {
                lock (syncRoot)
                {
                    ResultMessageItem.AssignResult(updatesBatchId, result, notifications, this);
                }
            }

            public void SendResult(bool isAcknowledgment = false)
            {
                lock (syncRoot)
                {
                    if (IsProcessed || (isAcknowledgment && IsAcknowledgmentSent))
                        return;

                    if (!IsAcknowledgmentSent)
                    {
                        foreach (var notification in ResultMessageItem.Notifications)
                        {
                            var aPubKey = ResultManager.Context.AccountStorage.GetAccount(notification.Key).Account.Pubkey;
                            ResultManager.Context.Notify(aPubKey, notification.Value.CreateEnvelope());
                        }
                    }

                    if (ResultMessageItem.AccountPubKey != null)
                        ResultManager.Context.Notify(ResultMessageItem.AccountPubKey, ResultMessageItem.Result.CreateEnvelope());

                    IsAcknowledgmentSent = true;
                }
            }

            public void SubmitTransaction()
            {
                try
                {
                    if (ResultMessageItem.Result.OriginalMessage.Message is RequestTransactionQuantum transactionQuantum)
                    {
                        if (!ResultManager.Context.PaymentProvidersManager.TryGetManager(transactionQuantum.ProviderId, out var paymentProvider))
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
                    var votesCount = ResultMessageItem.Result.Effects.Signatures.Count;

                    var originalEnvelope = ResultMessageItem.Result.OriginalMessage;
                    SequentialRequestMessage requestMessage = null;
                    if (originalEnvelope.Message is SequentialRequestMessage)
                        requestMessage = (SequentialRequestMessage)originalEnvelope.Message;
                    else if (originalEnvelope.Message is RequestQuantum)
                        requestMessage = ((RequestQuantum)originalEnvelope.Message).RequestEnvelope.Message as SequentialRequestMessage;

                    var exc = new Exception("Majority for quantum" +
                        $" {ResultMessageItem.Apex} ({requestMessage?.MessageType ?? originalEnvelope.Message.MessageType})" +
                        $" is unreachable. Results received count is {processedAuditors.Count}," +
                        $" valid results count is {votesCount}. The constellation collapsed.");
                    ResultManager.Context.StateManager.Failed(exc);
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

                    ResultManager.Context.PendingUpdatesManager.AddSignatures(ResultMessageItem.UpdatesBatchId, ResultMessageItem.Apex, resultMessages);
                }
            }

            private MajorityResults CheckMajority()
            {
                int requiredMajority = ResultManager.Context.GetMajorityCount(),
                    maxVotes = ResultManager.Context.GetTotalAuditorsCount();

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

        class ResultMessageItem
        {
            public ResultMessageItem(AuditorResultMessage result, RawPubKey auditor)
            {
                Apex = result.Apex;
                AddResultMessage(result, auditor);
            }

            public ResultMessageItem(uint updatesBatchId, QuantumResultMessageBase result, Dictionary<ulong, Message> notifications, ResultConsensusAggregate aggregate)
            {
                AssignResult(updatesBatchId, result, notifications, aggregate);
                Apex = ((Quantum)result.OriginalMessage.Message).Apex;
            }

            public void AssignResult(uint updatesBatchId, QuantumResultMessageBase result, Dictionary<ulong, Message> notifications, ResultConsensusAggregate aggregate)
            {
                UpdatesBatchId = updatesBatchId;
                Result = result ?? throw new ArgumentNullException(nameof(result));
                EffectsHash = Result.Quantum.EffectsHash;
                AccountPubKey = GetMessageAccount(aggregate.ResultManager.Context);
                Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
                AddOutrunSignatures(aggregate);
            }

            public void AddResultMessage(AuditorResultMessage result, RawPubKey auditor)
            {
                if (OutrunSignatures.ContainsKey(auditor))
                    return;
                OutrunSignatures.Add(auditor, result);
            }

            public uint UpdatesBatchId { get; private set; }

            public ulong Apex { get; }

            public QuantumResultMessageBase Result { get; private set; }

            public bool IsResultAssigned => Result != null;

            public Dictionary<RawPubKey, AuditorResultMessage> OutrunSignatures { get; } = new Dictionary<RawPubKey, AuditorResultMessage>();

            public byte[] EffectsHash { get; private set; }

            public Dictionary<ulong, Message> Notifications { get; private set; }

            public RawPubKey AccountPubKey { get; private set; }

            private void AddOutrunSignatures(ResultConsensusAggregate aggregate)
            {
                foreach (var result in OutrunSignatures)
                    aggregate.Add(result.Value, result.Key);
            }

            private RawPubKey GetMessageAccount(ExecutionContext context)
            {
                var originalEnvelope = Result.OriginalMessage;
                SequentialRequestMessage requestMessage = null;
                if (originalEnvelope.Message is SequentialRequestMessage)
                    requestMessage = (SequentialRequestMessage)originalEnvelope.Message;
                else if (originalEnvelope.Message is RequestQuantum)
                    requestMessage = ((RequestQuantum)originalEnvelope.Message).RequestEnvelope.Message as SequentialRequestMessage;

                if (requestMessage == null)
                    return null;
                return context.AccountStorage.GetAccount(requestMessage.Account).Account.Pubkey;
            }
        }
    }
}
