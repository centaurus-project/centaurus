using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class AccountDataRequestQuantum : RequestQuantumBase
    {
        [XdrField(0)]
        public byte[] PayloadHash { get; set; }
    }
}
