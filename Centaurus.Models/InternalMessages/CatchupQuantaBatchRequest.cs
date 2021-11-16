using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class CatchupQuantaBatchRequest: Message
    {
        [XdrField(0)]
        public ulong LastKnownApex { get; set; }
    }
}
