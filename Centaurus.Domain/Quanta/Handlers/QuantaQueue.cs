using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class QuantaQueue
    {
        /// <summary>
        /// The last processed quantum that was added on Alpha rising
        /// </summary>
        LinkedListNode<MessageEnvelope> lastAddedQuantum;

        readonly LinkedList<MessageEnvelope> messageQueue;

        public QuantaQueue(IEnumerable<MessageEnvelope> quanta)
        {
            if (quanta != null)
            {
                messageQueue = new LinkedList<MessageEnvelope>(quanta);
                lastAddedQuantum = messageQueue.Last;
            }
            else
                messageQueue = new LinkedList<MessageEnvelope>();
        }

        /// <summary>
        /// Adds an envelope to quanta queue
        /// </summary>
        /// <param name="envelope">Quantum to add</param>
        public void Enqueue(MessageEnvelope envelope)
        {
            messageQueue.AddLast(envelope);
        }

        /// <summary>
        /// Tries to remove and return the quantum at the beginning of the queue.
        /// </summary>
        /// <param name="envelope"></param>
        /// <returns>true if a quantum was find; otherwise, false.</returns>
        public bool TryDequeue(out MessageEnvelope envelope)
        {
            envelope = null;
            LinkedListNode<MessageEnvelope> quantumItem = messageQueue.First;
            if (quantumItem != null)
            {
                envelope = quantumItem.Value;
                messageQueue.Remove(quantumItem);

                //check if all restored quanta are processed
                if (quantumItem == lastAddedQuantum)
                    lastAddedQuantum = null;
                return true;
            }
            return false;
        }
    }
}
