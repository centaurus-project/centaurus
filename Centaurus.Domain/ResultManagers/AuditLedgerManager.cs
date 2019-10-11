using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditLedgerManager : MajorityManager
    {
        public async Task Add(MessageEnvelope envelope)
        {
            await Aggregate(envelope);
        }

        protected override Task OnResult(MajorityResults majorityResult, MessageEnvelope result)
        {
            base.OnResult(majorityResult, result);
            if (majorityResult != MajorityResults.Success)
            {
                logger.Info($"Majority result received ({majorityResult}).");
                return Task.CompletedTask;
            }
            var quantum = new LedgerCommitQuantum
            {
                Source = result
            };
            var ledgerCommitEnvelope = quantum.CreateEnvelope();
            Global.QuantumHandler.Handle(ledgerCommitEnvelope);
            return Task.CompletedTask;
        }
    }
}
