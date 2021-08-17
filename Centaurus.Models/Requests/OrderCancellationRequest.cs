using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class OrderCancellationRequest: SequentialRequestMessage
    {
        [XdrField(0)]
        public ulong OrderId { get; set; }
    }
}
