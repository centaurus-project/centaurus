using Centaurus.Xdr;
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
        public long TxCursor { get; set; }
    }
}
