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

        protected override async Task OnResult(MajorityResults majorityResult, MessageEnvelope result)
        {
            await base.OnResult(majorityResult, result);
            if (majorityResult != MajorityResults.Success)
            {
                logger.Info($"Majority result received ({majorityResult}).");
            }
            var quantum = new LedgerCommitQuantum
            {
                Source = result
            };
            var ledgerCommitEnvelope = quantum.CreateEnvelope();
            await Global.QuantumHandler.HandleAsync(ledgerCommitEnvelope);
        }
    }
}
