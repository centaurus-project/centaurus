using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class ResultManager : IDisposable
    {
        public AlphaContext Context { get; }

        public ResultManager(AlphaContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            InitTimers();
        }

        public void Register(MessageEnvelope envelope, byte[] messageHash, Dictionary<int, Message> notifications)
        {
            var resultMessageItem = new ResultMessageItem(envelope, messageHash, notifications, Context);
            if (!pendingAggregates.TryAdd(resultMessageItem.Apex, new ResultConsensusAggregate(resultMessageItem, this)))
                logger.Error("Unable to add result manager.");
        }

        public void Add(AuditorResultMessage resultMessage, RawPubKey auditor)
        {
            if (!pendingAggregates.TryGetValue(resultMessage.Apex, out var aggregate))
                return;

            //add the signature to the aggregate
            aggregate.Add(resultMessage, auditor);
        }

        /// <summary>
        /// Remove an aggregate by message id.
        /// </summary>
        /// <param name="messageId">Message id key.</param>
        public void Remove(long id)
        {
            if (!pendingAggregates.TryRemove(id, out _))
                logger.Trace($"Unable to remove item by id '{id}'");
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
            var resultsToSend = pendingAggregates.Values
                .Where(a => !a.IsAcknowledgmentSent && DateTime.UtcNow - a.CreatedAt > acknowledgmentTimeout)
                .ToArray();
            foreach (var resultItem in resultsToSend)
                Task.Factory.StartNew(() => resultItem.SendResult(true));
            acknowledgmentTimer.Start();
        }

        private TimeSpan aggregateLifeTime = new TimeSpan(0, 1, 0);

        private void Cleanup()
        {
            var now = DateTime.UtcNow;
            var itemsToRemove = pendingAggregates
                .Where(kv => now - kv.Value.CreatedAt > aggregateLifeTime)
                .Select(kv => kv.Key)
                .ToArray();

            foreach (var itemKey in itemsToRemove)
                Remove(itemKey);
            cleanupTimer?.Start();
        }

        private System.Timers.Timer cleanupTimer;
        private System.Timers.Timer acknowledgmentTimer;

        protected static Logger logger = LogManager.GetCurrentClassLogger();

        private ConcurrentDictionary<long, ResultConsensusAggregate> pendingAggregates = new ConcurrentDictionary<long, ResultConsensusAggregate>();

        class ResultConsensusAggregate
        {
            public ResultConsensusAggregate(ResultMessageItem resultMessageItem, ResultManager resultManager)
            {
                this.resultMessageItem = resultMessageItem;
                this.resultManager = resultManager;
                CreatedAt = DateTime.UtcNow;
                IsAcknowledgmentSent = resultMessageItem.AccountPubKey == null && resultMessageItem.Notifications.Count < 1;
            }

            public bool IsProcessed { get; private set; }
            public bool IsAcknowledgmentSent { get; private set; }

            public DateTime CreatedAt { get; }
            private ResultMessageItem resultMessageItem;
            private ResultManager resultManager;
            private List<RawPubKey> processedAuditors = new List<RawPubKey>();

            private object syncRoot = new { };

            public void Add(AuditorResultMessage result, RawPubKey auditor)
            {
                lock (syncRoot)
                {
                    if (IsProcessed || processedAuditors.Any(a => a.Equals(auditor)))
                        return;

                    processedAuditors.Add(auditor);

                    var signature = new Ed25519Signature { Signature = result.Signature, Signer = auditor };
                    if (signature.IsValid(resultMessageItem.Hash)
                        && !(resultMessageItem.IsTxResultMessage && result.TxSignature == null))
                    {
                        resultMessageItem.ResultEnvelope.Signatures.Add(signature);
                        if (resultMessageItem.IsTxResultMessage)
                        {
                            var txSignature = new Ed25519Signature { Signature = result.TxSignature, Signer = auditor };
                            ((ITransactionResultMessage)resultMessageItem.ResultMessage).TxSignatures.Add(txSignature);
                        }
                    }
                    var majorityResult = CheckMajority();
                    if (majorityResult == MajorityResults.Unknown)
                        return;

                    OnResult(majorityResult);
                    resultManager.Remove(resultMessageItem.Apex);
                    IsProcessed = true;
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
                        foreach (var notification in resultMessageItem.Notifications)
                        {
                            var aPubKey = resultManager.Context.AccountStorage.GetAccount(notification.Key).Account.Pubkey;
                            resultManager.Context.Notify(aPubKey, notification.Value.CreateEnvelope());
                        }
                    }

                    if (resultMessageItem.AccountPubKey != null)
                        resultManager.Context.Notify(resultMessageItem.AccountPubKey, resultMessageItem.ResultEnvelope);

                    IsAcknowledgmentSent = true;
                }
            }

            private void OnResult(MajorityResults majorityResult)
            {
                if (majorityResult == MajorityResults.Unreachable)
                {
                    var votesCount = resultMessageItem.ResultEnvelope.Signatures.Count - 1;//one signature belongs to Alpha

                    var originalEnvelope = resultMessageItem.ResultMessage.OriginalMessage;
                    SequentialRequestMessage requestMessage = null;
                    if (originalEnvelope.Message is SequentialRequestMessage)
                        requestMessage = (SequentialRequestMessage)originalEnvelope.Message;
                    else if (originalEnvelope.Message is RequestQuantum)
                        requestMessage = ((RequestQuantum)originalEnvelope.Message).RequestEnvelope.Message as SequentialRequestMessage;

                    var exc = new Exception($"Majority for quantum {resultMessageItem.Apex} ({requestMessage.MessageType}) is unreachable. Results received count is {processedAuditors.Count}, valid results count is {votesCount}. The constellation collapsed.");
                    logger.Error(exc);
                    resultManager.Context.AppState.State = ApplicationState.Failed;
                    throw exc;
                }

                SendResult();
            }

            private MajorityResults CheckMajority()
            {
                int requiredMajority = resultManager.Context.GetMajorityCount(),
                    maxVotes = resultManager.Context.GetTotalAuditorsCount();

                //if envelope contains Alpha signature we need to exclude it from count
                var votesCount = resultMessageItem.ResultEnvelope.Signatures.Count;
                if (resultMessageItem.ResultEnvelope.IsSignedBy(resultManager.Context.Settings.KeyPair))
                    votesCount--;

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
            public ResultMessageItem(MessageEnvelope resultEnvelope, byte[] messageHash, Dictionary<int, Message> notifications, AlphaContext context)
            {
                ResultEnvelope = resultEnvelope ?? throw new ArgumentNullException(nameof(resultEnvelope));
                Hash = messageHash;
                IsTxResultMessage = resultEnvelope.Message is ITransactionResultMessage;
                AccountPubKey = GetMessageAccount(context);
                Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            }

            public MessageEnvelope ResultEnvelope { get; }

            public bool IsTxResultMessage { get; }

            public ResultMessage ResultMessage => (ResultMessage)ResultEnvelope.Message;

            public long Apex => ResultMessage.MessageId;

            public byte[] Hash { get; }

            public Dictionary<int, Message> Notifications { get; }

            public RawPubKey AccountPubKey { get; }

            private RawPubKey GetMessageAccount(AlphaContext context)
            {
                var originalEnvelope = ResultMessage.OriginalMessage;
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
