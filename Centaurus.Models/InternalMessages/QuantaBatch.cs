using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class QuantaBatch : Message
    {
        [XdrField(0)]
        public List<PendingQuantum> Quanta { get; set; }

        [XdrField(1)]
        public bool HasMorePendingQuanta { get; set; }
    }
}
