using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class LedgerUpdateEffectProcessor : EffectProcessor<LedgerUpdateEffect>
    {
        private LedgerManager ledgerManager;

        public LedgerUpdateEffectProcessor(LedgerUpdateEffect effect, LedgerManager ledgerManager)
            : base(effect)
        {
            this.ledgerManager = ledgerManager;
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            ledgerManager.SetLedger(Effect.Ledger);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            ledgerManager.SetLedger(Effect.PrevLedger);
        }
    }
}
