using Centaurus.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    //TODO: add validation timeout
    //TODO: pending aggregates cleanup
    public abstract class MajorityManager
    {
        Dictionary<string, ConsensusAggregate> pendingAggregates = new Dictionary<string, ConsensusAggregate>();

        protected MessageEnvelope Aggregate(MessageEnvelope envelope)
        {
            var id = $"{(int)envelope.Message.MessageType}_{envelope.Message.MessageId}"; 
            //skip signatures that arrived after the consensus has been reached/failed.
            if (!pendingAggregates.TryGetValue(id, out var aggregate))
            {
                aggregate = new ConsensusAggregate();
                pendingAggregates.Add(id, aggregate);
            }
            //add the signature to the aggregate
            aggregate.Add(envelope);
            switch (aggregate.CheckMajority(out var consensus))
            {
                case MajorityResults.Unknown: return null;
                case MajorityResults.Success: return consensus;
            }
            throw new Exception("The constellation collapsed.");
        }

        /// <summary>
        /// Remove an aggregate by message id.
        /// </summary>
        /// <param name="messageId">Message id key.</param>
        public void Remove(string id)
        {
            pendingAggregates.Remove(id);
        }

        public class ConsensusAggregate
        {
            public ConsensusAggregate()
            {
                storage = new ConcurrentDictionary<byte[], MessageEnvelope>(new HashComparer());
            }

            private ConcurrentDictionary<byte[], MessageEnvelope> storage;

            private MessageEnvelope GetSignaturesContainer(byte[] messageHash, MessageEnvelope envelope)
            {
                if (!storage.TryGetValue(messageHash, out var envelopeAggregate))
                {
                    envelopeAggregate = new MessageEnvelope { Message = envelope.Message, Signatures = new List<Ed25519Signature>() };
                    if (!storage.TryAdd(messageHash, envelopeAggregate))
                    {
                        //retry in case of conflict
                        return GetSignaturesContainer(messageHash, envelope);
                    }
                }
                return envelopeAggregate;
            }

            public void Add(MessageEnvelope envelope)
            {
                var messageHash = envelope.ComputeMessageHash();
                var container = GetSignaturesContainer(messageHash, envelope);
                lock (container)
                {
                    container.AggregateEnvelop(envelope);
                }
            }

            public MajorityResults CheckMajority(out MessageEnvelope consensus)
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