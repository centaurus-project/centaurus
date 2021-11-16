using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class QuantumInfo
    {
        [XdrField(0)]
        public ulong Apex { get; set; }

        [XdrField(1)]
        public RequestInfoBase Request { get; set; }

        [XdrField(2)]
        public List<EffectsInfoBase> Items { get; set; }

        [XdrField(3)]
        public List<TinySignature> Proof { get; set; }

        [XdrField(4)]
        public long Timestamp { get; set; }
    }
}
