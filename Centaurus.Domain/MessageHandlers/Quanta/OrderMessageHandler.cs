using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class OrderMessageHandler : QuantumHandlerBase
    {
        public OrderMessageHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(OrderRequest).Name;
    }
}
