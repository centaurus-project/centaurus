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
            ledgerManager.SetLedger(Effect.Ledger);
        }

        public override void RevertEffect()
        {
            ledgerManager.SetLedger(Effect.PrevLedger);
        }

        public static LedgerUpdateEffectProcessor GetProcessor(ulong apex, long ledger, LedgerManager ledgerManager)
        {
            return GetProcessor(
                new LedgerUpdateEffect { Apex = apex, Ledger = ledger, PrevLedger = ledgerManager.Ledger },
                ledgerManager
            );
        }

        public static LedgerUpdateEffectProcessor GetProcessor(LedgerUpdateEffect effect, LedgerManager ledgerManager)
        {
            return new LedgerUpdateEffectProcessor(effect, ledgerManager);
        }
    }
}
