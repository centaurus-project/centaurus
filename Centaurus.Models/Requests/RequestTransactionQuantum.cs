using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class RequestTransactionQuantum: RequestQuantum
    {
        public override MessageTypes MessageType => MessageTypes.RequestTransactionQuantum;

        [XdrField(0)]
        public byte[] Transaction { get; set; }
    }
}
