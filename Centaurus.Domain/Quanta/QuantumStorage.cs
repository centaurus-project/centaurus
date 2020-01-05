using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace Centaurus.Domain
{
    //TODO: add save method, add cleanup method
    public class QuantumStorage
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public List<long> apexes = new List<long>();

        public List<MessageEnvelope> quanta = new List<MessageEnvelope>();

        public int QuantaCacheCapacity = 1_000_000;
        int capacityThreshold = 1000;

        public long CurrentApex { get; private set; }
        public byte[] LastQuantumHash { get; private set; }

        public QuantumStorage(long currentApex, byte[] lastQuantumHash)
        {
            CurrentApex = currentApex;
            LastQuantumHash = lastQuantumHash;
        }

        public void AddQuantum(MessageEnvelope envelope)
        {
            lock (this)
            {
                var quantum = (Quantum)envelope.Message;
                if (Global.IsAlpha)
                {
                    quantum.Apex = ++CurrentApex;
                    quantum.PrevHash = LastQuantumHash;
                    quantum.Timestamp = DateTime.UtcNow.Ticks;
                }
                else if (quantum.Apex == default) //when auditor receives quantum, the quantum should already contain apex
                    throw new Exception("Quantum has no apex");
                else
                    CurrentApex = quantum.Apex;

                LastQuantumHash = quantum.ComputeHash();
                apexes.Add(quantum.Apex);
                quanta.Add(envelope);
                if (apexes.Count == QuantaCacheCapacity + capacityThreshold) //remove oldest quanta
                {
                    apexes.RemoveRange(0, capacityThreshold);
                    quanta.RemoveRange(0, capacityThreshold);
                }
            }
        }

        /// <summary>
        /// Returns batch of quanta from specified apex (including).
        /// </summary>
        /// <param name="apexFrom">Batch start.</param>
        /// <param name="maxCount">Batch max size.</param>
        /// <param name="messageEnvelopes">Batch itself. Can be null.</param>
        /// <returns>True if data presented in the storage, otherwise false.</returns>
        public bool GetQuantaBacth(long apexFrom, int maxCount, out List<MessageEnvelope> messageEnvelopes)
        {
            lock (this)
            {
                messageEnvelopes = null;
                var apexIndex = apexes.IndexOf(apexFrom);
                if (apexIndex == -1)
                    return false;
                messageEnvelopes = quanta.Skip(apexIndex).Take(maxCount).ToList();

                var lastItem = messageEnvelopes.LastOrDefault();
                if (lastItem != null && !((Quantum)lastItem.Message).IsProcessed) //last item can still be processed
                    messageEnvelopes.RemoveAt(messageEnvelopes.Count - 1);

                return true;
            }
        }
    }
}
