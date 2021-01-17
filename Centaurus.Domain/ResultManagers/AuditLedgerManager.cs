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
        public void Add(MessageEnvelope envelope)
        {
            Aggregate(envelope);
        }

        protected override void OnResult(MajorityResults majorityResult, MessageEnvelope result)
        {
            base.OnResult(majorityResult, result);
            if (majorityResult != MajorityResults.Success)
            {
                logger.Info($"Majority result received ({majorityResult}).");
            }
            var quantum = new TxCommitQuantum
            {
                Source = result
            };
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var ledgerCommitEnvelope = quantum.CreateEnvelope();
                    await Global.QuantumHandler.HandleAsync(ledgerCommitEnvelope);
                }
                catch (Exception exc)
                {
                    logger.Error(exc, "Error on TxCommitQuantum handling.");
                    Global.AppState.State = ApplicationState.Failed;
                }
            });
        }
    }
}
