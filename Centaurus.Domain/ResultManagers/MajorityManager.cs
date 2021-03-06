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
    public abstract class MajorityManager : ContextualBase<AlphaContext>, IDisposable
    {
        public MajorityManager(AlphaContext context)
            : base(context)
        {
            InitCleanupTimer();
        }

        public void Add(IncomingMessage message)
        {
            var id = GetId(message.Envelope);
            ConsensusAggregate aggregate = GetConsensusAggregate(id);

            //add the signature to the aggregate
            aggregate.Add(message);
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
                    Remove(itemKey);
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
                    logger.Trace($"Unable to remove item by id '{id}'"); //it could be removed by timer
            }
        }

        protected virtual void OnResult(MajorityResults majorityResult, MessageEnvelope confirmation)
        {
            if (majorityResult == MajorityResults.Unreachable)
            {
                var exc = new Exception("Majority is unreachable. The constellation collapsed.");
                logger.Error(exc);
                Context.AppState.State = ApplicationState.Failed;
                throw exc;
            }
        }

        protected virtual byte[] GetHash(MessageEnvelope envelope)
        {
            return envelope.ComputeMessageHash();
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

            private Dictionary<byte[], MessageEnvelopeVote> storage = new Dictionary<byte[], MessageEnvelopeVote>(ByteArrayComparer.Default);

            public void Add(IncomingMessage message)
            {
                MessageEnvelope consensus = null;
                MajorityResults majorityResult = MajorityResults.Unknown;
                lock (syncRoot)
                {
                    if (!storage.TryGetValue(message.MessageHash, out var envelopeVote))//first result with such hash
                    {
                        envelopeVote = new MessageEnvelopeVote(message.Envelope, message.MessageHash);
                        storage.Add(message.MessageHash, envelopeVote);
                    }
                    else
                        envelopeVote.AddSignature(message.Envelope.Signatures[0]);

                    if (storage.Count > 1)
                    { }

                    if (IsProcessed)
                    {
                        if (storage.Select(e => e.Value.Signatures.Count).Sum() == ((AlphaStateManager)majorityManager.Context.AppState).ConnectedAuditorsCount) //remove if all auditors sent results
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
                int requiredMajority = majorityManager.Context.GetMajorityCount(),
                    maxVotes = majorityManager.Context.GetTotalAuditorsCount(),
                    maxConsensus = 0,
                    totalOpposition = 0;
                //try to find the majority
                foreach (var pair in storage)
                {
                    var votes = pair.Value.Signatures.Count;
                    //check if we have the majority
                    if (votes >= requiredMajority)
                    {
                        //add signatures to envelope
                        pair.Value.AggregateSignatures();

                        //return the messages for the consensus
                        consensus = pair.Value.Envelope;
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

            class MessageEnvelopeVote
            {
                public MessageEnvelopeVote(MessageEnvelope envelope, byte[] messageHash)
                {
                    Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
                    Hash = messageHash ?? throw new ArgumentNullException(nameof(messageHash));
                    var signature = envelope.Signatures[0];
                    Signatures = new Dictionary<RawPubKey, Ed25519Signature> { { signature.Signer, signature } };

                    //all signatures must be stored in Signatures prop
                    Envelope.Signatures.Clear();
                }

                public byte[] Hash { get; }

                public MessageEnvelope Envelope { get; }

                public Dictionary<RawPubKey, Ed25519Signature> Signatures { get; }

                public void AddSignature(Ed25519Signature signature)
                {
                    if (Signatures.ContainsKey(signature.Signer))
                        throw new InvalidOperationException($"Already contains signature from {signature.Signer}");

                    Signatures.Add(signature.Signer, signature);
                }

                public void AggregateSignatures()
                {
                    Envelope.Signatures.AddRange(Signatures.Values);
                }
            }
        }
    }
}