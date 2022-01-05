using Centaurus.Xdr;
using System;

namespace Centaurus.Models
{
    public class StateMessage : Message
    {
        [XdrField(0)]
        public ulong CurrentApex { get; set; }

        [XdrField(1)]
        public ulong LastPersistedApex { get; set; }

        [XdrField(2)]
        public int QuantaQueueLength { get; set; }

        [XdrField(3)]
        public State State { get; set; }

        [XdrField(4)]
        public long UpdateDate { get; set; }
    }
}