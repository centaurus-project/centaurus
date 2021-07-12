using Centaurus.Models;
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

        public void Add(ulong apex, QuantumResultMessage resultMessage, byte[] effectsHash, Dictionary<ulong, Message> notifications)
        {
            var aggregate = default(ResultConsensusAggregate);
            lock (pendingAggregatesSyncRoot)
            {
                if (!pendingAggregates.TryGetValue(apex, out aggregate))
                {
                    pendingAggregates.Add(apex, new ResultConsensusAggregate(resultMessage, effectsHash, notifications, this));
                    return;
                }
            }

            aggregate.Add(resultMessage, effectsHash, notifications);
        }

        public void Add(AuditorResultMessage resultMessage, RawPubKey auditor)
        {
            var aggregate = default(ResultConsensusAggregate);
            lock (pendingAggregatesSyncRoot)
            {
                if (!pendingAggregates.TryGetValue(resultMessage.Apex, out aggregate))
                {
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

            public ResultConsensusAggregate(QuantumResultMessage result, byte[] messageHash, Dictionary<ulong, Message> notifications, ResultManager resultManager)
                : this(resultManager)
            {
                ResultMessageItem = new ResultMessageItem(result, messageHash, notifications, this);
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

            private List<RawPubKey> processedAuditors = new List<RawPubKey>();

            private object syncRoot = new { };

            public void Add(AuditorResultMessage result, RawPubKey auditor)
            {
                lock (syncRoot)
                {
                    if (IsProcessed || processedAuditors.Any(a => a.Equals(auditor)))
                        return;

                    if (!ResultMessageItem.IsResultAssigned)
                    {
                        ResultMessageItem.AddResultMessage(result, auditor);
                        return;
                    }

                    processedAuditors.Add(auditor);

                    var signature = new Ed25519Signature { Signature = result.Signature, Signer = auditor };
                    if (signature.IsValid(ResultMessageItem.Hash)
                        && !(ResultMessageItem.IsTxResultMessage && result.TxSignature == null))
                    {
                        ResultMessageItem.Result.Effects.Signatures.Add(signature);
                        if (ResultMessageItem.IsTxResultMessage)
                        {
                            var txSignature = new TxSignature { Signature = result.TxSignature, Signer = result.TxSigner };
                            ((TransactionResultMessage)ResultMessageItem.Result).TxSignatures.Add(txSignature);
                        }
                    }
                    var majorityResult = CheckMajority();
                    if (majorityResult == MajorityResults.Unknown)
                        return;

                    OnResult(majorityResult);
                    ResultManager.Remove(ResultMessageItem.Apex);
                    IsProcessed = true;
                }
            }

            public void Add(QuantumResultMessage result, byte[] messageHash, Dictionary<ulong, Message> notifications)
            {
                lock (syncRoot)
                {
                    ResultMessageItem.AssignResult(result, messageHash, notifications, this);
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
                        //TODO: Refactor it now!!
                        var paymentProviderId = ((WithdrawalRequest)transactionQuantum.RequestMessage).PaymentProvider;


                        if (!ResultManager.Context.PaymentProvidersManager.TryGetManager(paymentProviderId, out var paymentProvider))
                            throw new Exception($"Unable to find manager {paymentProviderId}");
                        paymentProvider.SubmitTransaction(transactionQuantum.Transaction, ((TransactionResultMessage)ResultMessageItem.Result).TxSignatures);
                    }
                }
                catch (Exception exc)
                {
                    logger.Error(new Exception("Error on submit", exc));
                    ResultManager.Context.AppState.State = ApplicationState.Failed;
                    throw exc;
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
                    logger.Error(exc);
                    ResultManager.Context.AppState.State = ApplicationState.Failed;
                    throw exc;
                }
                SubmitTransaction();
                SendResult();
            }

            private MajorityResults CheckMajority()
            {
                int requiredMajority = ResultManager.Context.GetMajorityCount(),
                    maxVotes = ResultManager.Context.GetTotalAuditorsCount();

                var votesCount = ResultMessageItem.Result.Effects.Signatures.Count;

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

            public ResultMessageItem(QuantumResultMessage result, byte[] messageHash, Dictionary<ulong, Message> notifications, ResultConsensusAggregate aggregate)
            {
                AssignResult(result, messageHash, notifications, aggregate);
                Apex = ((Quantum)result.OriginalMessage.Message).Apex;
            }

            public void AssignResult(QuantumResultMessage result, byte[] messageHash, Dictionary<ulong, Message> notifications, ResultConsensusAggregate aggregate)
            {
                Result = result ?? throw new ArgumentNullException(nameof(result));
                Hash = messageHash;
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

            public ulong Apex { get; }

            public QuantumResultMessage Result { get; private set; }

            public bool IsTxResultMessage => Result is TransactionResultMessage;

            public bool IsResultAssigned => Result != null;

            public Dictionary<RawPubKey, AuditorResultMessage> OutrunSignatures { get; } = new Dictionary<RawPubKey, AuditorResultMessage>();

            public byte[] Hash { get; private set; }

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
