using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace Centaurus.Domain
{
    public abstract class QuantumStorageBase
    {
        public long CurrentApex { get; protected set; }
        public byte[] LastQuantumHash { get; protected set; }

        public QuantumStorageBase(long currentApex, byte[] lastQuantumHash)
        {
            CurrentApex = currentApex;
            LastQuantumHash = lastQuantumHash;
        }

        public abstract void AddQuantum(MessageEnvelope envelope, byte[] hash);
    }

    public class AlphaQuantumStorage : QuantumStorageBase
    {
        private List<long> apexes = new List<long>();

        private List<MessageEnvelope> quanta = new List<MessageEnvelope>();

        private int QuantaCacheCapacity = 1_000_000;
        private int capacityThreshold = 100_000;

        public AlphaQuantumStorage(long currentApex, byte[] lastQuantumHash)
            : base(currentApex, lastQuantumHash)
        {
        }

        public override void AddQuantum(MessageEnvelope envelope, byte[] hash)
        {
            lock (this)
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
        public bool GetQuantaBacth(long apexFrom, int maxCount, out List<MessageEnvelope> messageEnvelopes)
        {
            lock (this)
            {
                messageEnvelopes = null;
                var apexIndex = apexes.IndexOf(apexFrom);
                if (apexIndex == -1)
                    return false;
                messageEnvelopes = quanta.Skip(apexIndex).Take(maxCount).ToList();
                return true;
            }
        }
    }

    public class AuditorQuantumStorage : QuantumStorageBase
    {
        public AuditorQuantumStorage(long currentApex, byte[] lastQuantumHash)
            : base(currentApex, lastQuantumHash)
        {

        }

        public override void AddQuantum(MessageEnvelope envelope, byte[] hash)
        {
            lock (this)
            {
                var quantum = (Quantum)envelope.Message;
                if (quantum.Apex < 1) //when auditor receives quantum, the quantum should already contain apex
                    throw new Exception("Quantum has no apex");
                CurrentApex = quantum.Apex;
                LastQuantumHash = hash;
            }
        }
    }
}