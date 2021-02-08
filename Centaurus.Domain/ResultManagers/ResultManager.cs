using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            lock (syncRoot)
            {
                var resultMessageItem = new ResultMessageItem(resultMessage, notifications);
                pendingAggregates.Add(resultMessageItem.Apex, new ResultConsensusAggregate(resultMessageItem, this));
            }
        }

        public void Add(AuditorResultMessage resultMessage, RawPubKey auditor)
        {
            var aggregate = default(ResultConsensusAggregate);
            lock (syncRoot)
                if (!pendingAggregates.TryGetValue(resultMessage.Apex, out aggregate))
                    return;

            //add the signature to the aggregate
            aggregate.Add(resultMessage, auditor);
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
            lock (syncRoot)
            {
                var acknowledgmentTimeout = TimeSpan.FromMilliseconds(100);
                foreach (var aggregate in pendingAggregates)
                    if (!aggregate.Value.IsAcknowledgmentSent && DateTime.UtcNow - aggregate.Value.CreatedAt > acknowledgmentTimeout)
                        aggregate.Value.SendResult();
            }
            acknowledgmentTimer.Start();
        }

        private TimeSpan aggregateLifeTime = new TimeSpan(0, 1, 0);

        private void CleanupTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (syncRoot)
            {
                var now = DateTime.UtcNow;
                var itemsToRemove = pendingAggregates
                    .Where(kv => now - kv.Value.CreatedAt > aggregateLifeTime)
                    .Select(kv => kv.Key)
                    .ToArray();

                foreach (var itemKey in itemsToRemove)
                    Remove(itemKey);
            }
            cleanupTimer?.Start();
        }

        private System.Timers.Timer cleanupTimer;
        private System.Timers.Timer acknowledgmentTimer;

        protected static Logger logger = LogManager.GetCurrentClassLogger();

        private Dictionary<long, ResultConsensusAggregate> pendingAggregates = new Dictionary<long, ResultConsensusAggregate>();

        private object syncRoot = new { };

        /// <summary>
        /// Remove an aggregate by message id.
        /// </summary>
        /// <param name="messageId">Message id key.</param>
        public void Remove(long id)
        {
            lock (syncRoot)
            {
                if (!pendingAggregates.Remove(id))
                    logger.Error($"Unable to remove item by id '{id}'");
            }
        }

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

            private object syncRoot = new { };

            public DateTime CreatedAt { get; }
            private ResultMessageItem resultMessageItem;
            private ResultManager resultManager;
            private List<RawPubKey> processedAuditors = new List<RawPubKey>();

            public void Add(AuditorResultMessage result, RawPubKey auditor)
            {
                lock (syncRoot)
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
                lock (syncRoot)
                {
                    if (IsProcessed)
                        return;
                    var originalEnvelope = resultMessageItem.ResultMessage.OriginalMessage;
                    NonceRequestMessage requestMessage = null;
                    if (originalEnvelope.Message is NonceRequestMessage)
                        requestMessage = (NonceRequestMessage)originalEnvelope.Message;
                    else if (originalEnvelope.Message is RequestQuantum)
                        requestMessage = ((RequestQuantum)originalEnvelope.Message).RequestEnvelope.Message as NonceRequestMessage;

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
                Hash = resultMessage.ComputeHash();
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
