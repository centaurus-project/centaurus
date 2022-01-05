using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class ConstellationRequestMessage: Message
    {
        [XdrField(0)]
        public ulong LastUpdateApex { get; set; }
    }
}
