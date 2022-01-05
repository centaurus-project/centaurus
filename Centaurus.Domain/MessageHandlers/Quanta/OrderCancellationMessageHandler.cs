using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    internal class OrderCancellationMessageHandler : QuantumHandlerBase
    {
        public OrderCancellationMessageHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(OrderCancellationRequest).Name;
    }
}
