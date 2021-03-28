using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain.Quanta.Contexts
{
    public class OrderCancellationProcessorContext : ProcessorContext
    {
        public OrderCancellationProcessorContext(EffectProcessorsContainer effectProcessors) 
            : base(effectProcessors)
        {
        }

        public OrderbookBase Orderbook { get; set; }

        private Order order;
        public Order Order 
        { 
            get
            {
                return order;
            }
            set
            {
                order = value;
                if (order != null)
                {
                    var decodedId = OrderIdConverter.Decode(order.OrderId);
                    OrderSide = decodedId.Side;
                    Asset = decodedId.Asset;
                }
            }
        }

        public OrderSide OrderSide { get; private set; }

        public int Asset { get; private set; }
    }
}
