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
    //TODO: add validation timeout
    //TODO: pending aggregates cleanup
    public abstract class MajorityManager
    {
        protected static Logger logger = LogManager.GetCurrentClassLogger();

        protected static string GetId(MessageEnvelope envelope)
        {
            return $"{(int)envelope.Message.MessageType}_{envelope.Message.MessageId}";
        }

        Dictionary<string, ConsensusAggregate> pendingAggregates = new Dictionary<string, ConsensusAggregate>();

        object syncRoot = new { };

        private ConsensusAggregate GetConsensusAggregate(string id)
        {
            lock (syncRoot)
            {
                if (!pendingAggregates.ContainsKey(id))
                    pendingAggregates[id] = new ConsensusAggregate(this);
                return pendingAggregates[id];
            }
        }

        protected async Task Aggregate(MessageEnvelope envelope)
        {
            var id = GetId(envelope);
            ConsensusAggregate aggregate = GetConsensusAggregate(id);

            //add the signature to the aggregate
            await aggregate.Add(envelope);
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

        protected virtual Task OnResult(MajorityResults majorityResult, MessageEnvelope confirmation)
        {
            if (majorityResult == MajorityResults.Unreachable)
            {
                logger.Error("Majority is unreachable. The constellation collapsed.");
                Global.AppState.State = ApplicationState.Failed;
            }
            return Task.CompletedTask;
        }

        public class ConsensusAggregate
        {
            public ConsensusAggregate(MajorityManager majorityManager)
            {
                this.majorityManager = majorityManager;
            }

            private object syncRoot = new { };

            private MajorityManager majorityManager;

            private bool alreadyHasResult = false;

            private Dictionary<byte[], MessageEnvelope> storage = new Dictionary<byte[], MessageEnvelope>(new ByteArrayComparer());

            public async Task Add(MessageEnvelope envelope)
            {
                MessageEnvelope consensus = null;
                MajorityResults majorityResult = MajorityResults.Unknown;
                lock (syncRoot)
                {
                    if (alreadyHasResult)
                        return;
                    var envelopeHash = envelope.ComputeMessageHash();
                    if (!storage.ContainsKey(envelopeHash))//first result with such hash
                        storage[envelopeHash] = envelope;
                    else
                        storage[envelopeHash].AggregateEnvelopUnsafe(envelope);//we can use AggregateEnvelopUnsafe, we compute hash for every envelope above
                    majorityResult = CheckMajority(out consensus);
                    if (majorityResult != MajorityResults.Unknown)
                        alreadyHasResult = true;
                }
                if (majorityResult != MajorityResults.Unknown)
                    await majorityManager.OnResult(majorityResult, consensus);
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
                if (maxVotes - totalOpposition < requiredMajority)
                {//no chances to reach the majority
                    return MajorityResults.Unreachable;
                }
                //not enough votes to decided whether the consensus can be reached or not
                return MajorityResults.Unknown;
            }
        }
    }
}