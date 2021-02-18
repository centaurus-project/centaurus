using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public class ResultManager : IDisposable
    {
        public ResultManager()
        {
            InitCleanupTimer();
        }

        public void Register(ResultMessage resultMessage, Dictionary<int, Message> notifications)
        {
            var resultMessageItem = new ResultMessageItem(resultMessage, notifications);
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

        private void InitCleanupTimer()
        {
            cleanupTimer = new System.Timers.Timer();
            cleanupTimer.Interval = 30 * 1000;
            cleanupTimer.AutoReset = false;
            cleanupTimer.Elapsed += CleanupTimer_Elapsed;
            cleanupTimer.Start();

            acknowledgmentTimer = new System.Timers.Timer();
            acknowledgmentTimer.Interval = 200;
            acknowledgmentTimer.AutoReset = false;
            acknowledgmentTimer.Elapsed += AcknowledgmentTimer_Elapsed;
            acknowledgmentTimer.Start();
        }

        private void AcknowledgmentTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            var acknowledgmentTimeout = TimeSpan.FromMilliseconds(100);
            var resultsToSend = pendingAggregates.Values
                .Where(a => !a.IsAcknowledgmentSent && DateTime.UtcNow - a.CreatedAt > acknowledgmentTimeout)
                .ToArray();
            foreach (var resultItem in resultsToSend)
                resultItem.SendResult();
            acknowledgmentTimer.Start();
        }

        private TimeSpan aggregateLifeTime = new TimeSpan(0, 1, 0);

        private void CleanupTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
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
            }

            public bool IsProcessed { get; private set; }
            public bool IsAcknowledgmentSent { get; private set; }

            public DateTime CreatedAt { get; }
            private ResultMessageItem resultMessageItem;
            private ResultManager resultManager;
            private List<RawPubKey> processedAuditors = new List<RawPubKey>();

            public void Add(AuditorResultMessage result, RawPubKey auditor)
            {
                lock (this)
                {
                    if (IsProcessed || processedAuditors.Any(a => a.Equals(auditor)))
                        return;

                    processedAuditors.Add(auditor);

                    var signature = new Ed25519Signature { Signature = result.Signature, Signer = auditor };
                    if (signature.IsValid(resultMessageItem.Hash))
                        resultMessageItem.ResultEnvelope.Signatures.Add(signature);

                    var majorityResult = CheckMajority();
                    if (majorityResult == MajorityResults.Unknown)
                        return;

                    OnResult(majorityResult);
                    resultManager.Remove(resultMessageItem.Apex);
                    IsProcessed = true;
                }
            }

            public void SendResult()
            {
                lock (this)
                {
                    if (IsProcessed)
                        return;
                    var originalEnvelope = resultMessageItem.ResultMessage.OriginalMessage;
                    SequentialRequestMessage requestMessage = null;
                    if (originalEnvelope.Message is SequentialRequestMessage)
                        requestMessage = (SequentialRequestMessage)originalEnvelope.Message;
                    else if (originalEnvelope.Message is RequestQuantum)
                        requestMessage = ((RequestQuantum)originalEnvelope.Message).RequestEnvelope.Message as SequentialRequestMessage;

                    if (requestMessage != null)
                    {
                        var aPubKey = Global.AccountStorage.GetAccount(requestMessage.Account).Account.Pubkey;
                        Notifier.Notify(aPubKey, resultMessageItem.ResultEnvelope);
                    }
                    if (!IsAcknowledgmentSent)
                    {
                        foreach (var notification in resultMessageItem.Notifications)
                        {
                            var aPubKey = Global.AccountStorage.GetAccount(notification.Key).Account.Pubkey;
                            Notifier.Notify(aPubKey, notification.Value.CreateEnvelope());
                        }
                        IsAcknowledgmentSent = true;
                    }
                }
            }

            private void OnResult(MajorityResults majorityResult)
            {
                if (majorityResult == MajorityResults.Unreachable)
                {
                    var exc = new Exception("Majority is unreachable. The constellation collapsed.");
                    logger.Error(exc);
                    Global.AppState.State = ApplicationState.Failed;
                    throw exc;
                }

                SendResult();
            }

            private MajorityResults CheckMajority()
            {
                int requiredMajority = MajorityHelper.GetMajorityCount(),
                    maxVotes = MajorityHelper.GetTotalAuditorsCount();
                //try to find the majority
                var votesCount = resultMessageItem.ResultEnvelope.Signatures.Count;

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
            public ResultMessageItem(ResultMessage resultMessage, Dictionary<int, Message> notifications)
            {
                if (resultMessage == null)
                    throw new ArgumentNullException(nameof(resultMessage));
                ResultEnvelope = resultMessage.CreateEnvelope();
                Hash = ResultEnvelope.ComputeMessageHash();
                Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            }

            public MessageEnvelope ResultEnvelope { get; }

            public ResultMessage ResultMessage => (ResultMessage)ResultEnvelope.Message;

            public long Apex => ResultMessage.MessageId;

            public byte[] Hash { get; }

            public Dictionary<int, Message> Notifications { get; }
        }
    }
}