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

        public void AddQuantum(Quantum quantum, byte[] hash)
        {
            lock (syncRoot)
            {
                if (quantum.Apex < 1)
                    throw new Exception("Quantum has no apex");

                CurrentApex = quantum.Apex;
                LastQuantumHash = hash;
                apexes.Add(quantum.Apex);
                quanta.Add(quantum);
                if (apexes.Count >= (QuantaCacheCapacity + capacityThreshold)) //remove oldest quanta
                {
                    apexes.RemoveRange(0, capacityThreshold);
                    quanta.RemoveRange(0, capacityThreshold);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="apexFrom">Batch start excluding.</param>
        /// <param name="maxCount">Batch max size.</param>
        /// <param name="quanta">Batch itself. Can be null.</param>
        /// <returns>True if data presented in the storage, otherwise false.</returns>
        public bool GetQuantaBacth(ulong apexFrom, int maxCount, out List<Quantum> quantaBatch)
        {
            lock (syncRoot)
            {
                quantaBatch = null;
                var apexIndex = apexes.IndexOf(apexFrom + 1);
                if (apexIndex == -1)
                    return false;
                quantaBatch = quanta.Skip(apexIndex).Take(maxCount).ToList();
                return true;
            }
        }

        private List<ulong> apexes = new List<ulong>();

        private List<Quantum> quanta = new List<Quantum>();

        private int QuantaCacheCapacity = 1_000_000;
        private int capacityThreshold = 100_000;

        private object syncRoot = new { };
    }
}