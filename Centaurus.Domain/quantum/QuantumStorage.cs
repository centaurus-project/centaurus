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

        Dictionary<ulong, MessageEnvelope> storage = new Dictionary<ulong, MessageEnvelope>();

        Timer cleanUpTimer;

        //cleanup interval in seconds
        int cleanupInterval = 60;
        int quantumsInMemoryPeriod = 5 * 60 * 1000;

        public ulong CurrentApex { get; private set; } = 0;
        public QuantumStorage(ulong currentApex)
        {
            CurrentApex = currentApex;
#if !DEBUG
            cleanUpTimer = new Timer();
            cleanUpTimer.Interval = cleanupInterval * 1000;
            cleanUpTimer.AutoReset = false;
            cleanUpTimer.Elapsed += CleanUp;
            cleanUpTimer.Start();
#endif
        }

        private void CleanUp(object sender, ElapsedEventArgs e)
        {
            try
            {
                var currentTicks = (ulong)DateTime.UtcNow.Ticks;
                var quantumsToCleanup = storage
                    .Where(q => new TimeSpan((long)(currentTicks - ((Quantum)q.Value.Message).Timestamp)).TotalMilliseconds > quantumsInMemoryPeriod)
                    .Select(q => q.Key);

                foreach (var q in quantumsToCleanup)
                    storage.Remove(q);
            }
            catch (Exception exc)
            {
                logger.Error(exc);
            }
            finally
            {
                cleanUpTimer.Start();
            }
        }

        public void AddQuantum(MessageEnvelope envelope)
        {
            var quantum = (Quantum)envelope.Message;
            if (Global.IsAlpha)
                quantum.Apex = ++CurrentApex;
            else if (quantum.Apex == default) //when auditor receives quantum, the quantum should already contain apex
                throw new Exception("Quantum has no apex");
            else
                CurrentApex = quantum.Apex;
            quantum.Timestamp = (ulong)DateTime.UtcNow.Ticks;
            storage.Add(quantum.Apex, envelope);
        }

        public MessageEnvelope GetQuantum(ulong apex)
        {
            if (!storage.ContainsKey(apex))
                throw new Exception($"Quantum {apex} was not found");
            return storage[apex];
        }

        public IEnumerable<MessageEnvelope> GetAllQuantums(ulong apexCursor = 0)
        {
            if (apexCursor == 0)
                return storage.Values;
            return storage.Values.Where(q => ((Quantum)q.Message).Apex > apexCursor);
        }
    }
}
