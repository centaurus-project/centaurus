using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class OrderCancellationMessageHandler : QuantumHandlerBase
    {
        public OrderCancellationMessageHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.OrderCancellationRequest;
    }
}
