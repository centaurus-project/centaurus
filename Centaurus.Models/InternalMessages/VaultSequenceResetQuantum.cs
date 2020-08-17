using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class VaultSequenceResetQuantum : Quantum
    {
        public override MessageTypes MessageType => MessageTypes.VaultSequenceResetQuantum;

        [XdrField(0)]
        public long LastSubmittedWithdrawalApex { get; set; }

        [XdrField(1)]
        public long VaultSequence { get; set; }
    }
}
