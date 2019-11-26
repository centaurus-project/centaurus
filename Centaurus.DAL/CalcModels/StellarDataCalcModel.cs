using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL
{
    public class StellarDataCalcModel: BaseCalcModel
    {
        public StellarDataCalcModel(long ledger, long vaultSequence)
        {
            Ledger = ledger;
            VaultSequence = vaultSequence;
        }
        public long Ledger { get; set; }
        public long VaultSequence { get; set; }
    }
}
