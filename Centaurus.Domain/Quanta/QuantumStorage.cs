using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace Centaurus.Domain
{
    public class QuantumStorage
    {
        public ulong CurrentApex { get; protected set; }
        public byte[] LastQuantumHash { get; protected set; }

        public void Init(ulong currentApex, byte[] lastQuantumHash)
        {
            CurrentApex = currentApex;
            LastQuantumHash = lastQuantumHash;
        }
        public void AddQuantum(MessageEnvelope envelope, byte[] hash)
        {
            lock (syncRoot)
            {
                var quantum = (Quantum)envelope.Message;
                if (quantum.Apex < 1)
                    throw new Exception("Quantum has no apex");

                CurrentApex = quantum.Apex;
                LastQuantumHash = hash;
                apexes.Add(quantum.Apex);
                quanta.Add(envelope);
                if (apexes.Count >= (QuantaCacheCapacity + capacityThreshold)) //remove oldest quanta
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
        public bool GetQuantaBacth(ulong apexFrom, int maxCount, out List<MessageEnvelope> messageEnvelopes)
        {
            lock (syncRoot)
            {
                messageEnvelopes = null;
                var apexIndex = apexes.IndexOf(apexFrom);
                if (apexIndex == -1)
                    return false;
                messageEnvelopes = quanta.Skip(apexIndex).Take(maxCount).ToList();
                return true;
            }
        }

        private List<ulong> apexes = new List<ulong>();

        private List<MessageEnvelope> quanta = new List<MessageEnvelope>();

        private int QuantaCacheCapacity = 1_000_000;
        private int capacityThreshold = 100_000;

        private object syncRoot = new { };
    }
}