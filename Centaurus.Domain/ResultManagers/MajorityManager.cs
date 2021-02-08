using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class MajorityManager : IDisposable
    {
        public MajorityManager()
        {
            InitCleanupTimer();
        }

        public void Add(MessageEnvelope envelope)
        {
            var id = GetId(envelope);
            ConsensusAggregate aggregate = GetConsensusAggregate(id);

            //add the signature to the aggregate
            aggregate.Add(envelope);
        }

        public virtual void Dispose()
        {
            cleanupTimer?.Stop();
            cleanupTimer?.Dispose();
            cleanupTimer = null;
        }

        private void InitCleanupTimer()
        {
            cleanupTimer = new System.Timers.Timer();
            cleanupTimer.Interval = 30 * 1000;
            cleanupTimer.AutoReset = false;
            cleanupTimer.Elapsed += CleanupTimer_Elapsed;
            cleanupTimer.Start();
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
                    pendingAggregates.Remove(itemKey);
            }
            cleanupTimer?.Start();
        }

        private System.Timers.Timer cleanupTimer;

        protected static Logger logger = LogManager.GetCurrentClassLogger();

        protected static string GetId(MessageEnvelope envelope)
        {
            return $"{(int)envelope.Message.MessageType}_{envelope.Message.MessageId}";
        }

        private Dictionary<string, ConsensusAggregate> pendingAggregates = new Dictionary<string, ConsensusAggregate>();

        private object syncRoot = new { };

        private ConsensusAggregate GetConsensusAggregate(string id)
        {
            lock (syncRoot)
            {
                if (!pendingAggregates.TryGetValue(id, out var consensusAggregate))
                {
                    consensusAggregate = new ConsensusAggregate(id, this);
                    pendingAggregates[id] = consensusAggregate;
                }
                return consensusAggregate;
            }
        }

        /// <summary>
        /// Remove an aggregate by message id.
        /// </summary>
        /// <param name="messageId">Message id key.</param>
        public void Remove(string id)
        {
            lock (syncRoot)
            {
                if (pendingAggregates.Remove(id))
                    logger.Error($"Unable to remove item by id '{id}'");
                else
                    logger.Trace($"Item with id '{id}' is removed");
            }
        }

        protected virtual void OnResult(MajorityResults majorityResult, MessageEnvelope confirmation)
        {
            if (majorityResult == MajorityResults.Unreachable)
            {
                var exc = new Exception("Majority is unreachable. The constellation collapsed.");
                logger.Error(exc);
                Global.AppState.State = ApplicationState.Failed;
                throw exc;
            }
        }

        protected virtual byte[] GetHash(MessageEnvelope envelope)
        {
            return envelope.Message.ComputeHash();
        }

        public class ConsensusAggregate
        {
            public ConsensusAggregate(string id, MajorityManager majorityManager)
            {
                this.Id = id;
                this.majorityManager = majorityManager;
                this.CreatedAt = DateTime.UtcNow;
            }

            public readonly DateTime CreatedAt;
            public bool IsProcessed { get; private set; }

            private object syncRoot = new { };

            public string Id { get; }

            private MajorityManager majorityManager;

            private Dictionary<byte[], MessageEnvelope> storage = new Dictionary<byte[], MessageEnvelope>(ByteArrayComparer.Default);

            public void Add(MessageEnvelope envelope)
            {
                MessageEnvelope consensus = null;
                MajorityResults majorityResult = MajorityResults.Unknown;
                lock (syncRoot)
                {
                    var envelopeHash = majorityManager.GetHash(envelope);
                    if (!storage.TryGetValue(envelopeHash, out var resultStorageEnvelope))//first result with such hash
                    {
                        resultStorageEnvelope = envelope;
                        storage[envelopeHash] = envelope;
                    }
                    else
                        resultStorageEnvelope.AggregateEnvelopUnsafe(envelope);//we can use AggregateEnvelopUnsafe, we compute hash for every envelope above

                    if (IsProcessed)
                    {
                        if (resultStorageEnvelope.Signatures.Count == ((AlphaStateManager)Global.AppState).ConnectedAuditorsCount) //remove if all auditors sent results
                        {
                            majorityManager?.Remove(Id);
                        }
                        return;
                    }

                    majorityResult = CheckMajority(out consensus);
                    if (majorityResult != MajorityResults.Unknown)
                    {
                        majorityManager.OnResult(majorityResult, consensus);
                        IsProcessed = true;
                    }
                }
            }

            private MajorityResults CheckMajority(out MessageEnvelope consensus)
            {
                //TODO: remove the item from storage once the majority succeeded of failed.
                int requiredMajority = MajorityHelper.GetMajorityCount(),
                    maxVotes = MajorityHelper.GetTotalAuditorsCount(),
                    maxConsensus = 0,
                    totalOpposition = 0;
                //try to find the majority
                foreach (var pair in storage)
                {
                    var votes = pair.Value.Signatures.Count;
                    //check if we have the majority
                    if (votes >= requiredMajority)
                    {
                        //return the messages for the consensus
                        consensus = pair.Value;
                        return MajorityResults.Success;
                    }
                    //check whether a current message is a potential consensus candidate
                    if (votes > maxConsensus)
                    {
                        //previous consensus candidate won't be able to get the majority - swap it
                        totalOpposition += maxConsensus;
                        maxConsensus = votes;
                    }
                }
                //failed to rich consensus
                consensus = null;

                var maxPossibleVotes = (maxVotes - totalOpposition) + maxConsensus;

                if (maxPossibleVotes < requiredMajority)
                {//no chances to reach the majority
                    return MajorityResults.Unreachable;
                }
                //not enough votes to decided whether the consensus can be reached or not
                return MajorityResults.Unknown;
            }
        }
    }
}