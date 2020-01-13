using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class ConstellationInitQuantum : ConstellationSettingsQuantum
    {
        public override MessageTypes MessageType => MessageTypes.ConstellationInitQuantum;

        [XdrField(0)]
        public long Ledger { get; set; }

        [XdrField(1)]
        public long VaultSequence { get; set; }
    }
}
