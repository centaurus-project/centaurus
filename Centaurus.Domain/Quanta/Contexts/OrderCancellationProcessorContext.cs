using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain.Quanta.Contexts
{
    public class OrderCancellationProcessorContext : RequestContext
    {
        public OrderCancellationProcessorContext(ExecutionContext context, Quantum quantum, AccountWrapper account) 
            : base(context, quantum, account)
        {
        }

        public OrderbookBase Orderbook { get; set; }

        public OrderWrapper OrderWrapper { get; set; }
    }
}
