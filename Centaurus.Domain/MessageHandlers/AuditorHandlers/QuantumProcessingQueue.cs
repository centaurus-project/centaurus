using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class QuantumProcessingQueue
    {
        public QuantumProcessingQueue(long initApex)
        {
            LastAddedApex = initApex;
        }


        Dictionary<long, MessageEnvelope> queue = new Dictionary<long, MessageEnvelope>();

        public long LastAddedApex { get; private set; }

        public void Add(MessageEnvelope envelope)
        {
            var quantum = envelope.Message as Quantum;
            lock (this)
            {
                if (!queue.ContainsKey(quantum.Apex))
                    queue.Add(quantum.Apex, envelope);
                else
                    queue[quantum.Apex] = envelope;

                LastAddedApex = quantum.Apex;
            }
        }

        public bool TryGet(long apex, out MessageEnvelope envelope)
        {
            envelope = null;
            lock (this)
            {
                if (queue.ContainsKey(apex))
                {
                    envelope = queue[apex];
                    return true;
                }
            }
            return false;
        }


        public bool TryRemove(long apex, out MessageEnvelope envelope)
        {
            envelope = null;
            lock (this)
            {
                if (queue.ContainsKey(apex))
                {
                    envelope = queue[apex];
                    return queue.Remove(apex);
                }
            }
            return false;
        }

        public bool ContainsKey(long apex)
        {
            lock (this)
                return queue.ContainsKey(apex);
        }
    }
}
