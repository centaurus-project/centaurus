using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Models
{
    public class OrderCancelationRequest: RequestMessage
    {
        public override MessageTypes MessageType => MessageTypes.OrderCancellationRequest;

        [XdrField(0)]
        public ulong OrderId { get; set; }
    }
}
