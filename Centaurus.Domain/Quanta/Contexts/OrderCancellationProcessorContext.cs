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
        public OrderCancellationProcessorContext(EffectProcessorsContainer effectProcessors) 
            : base(effectProcessors)
        {
        }

        public OrderbookBase Orderbook { get; set; }

        private OrderWrapper orderWrapper;
        public OrderWrapper OrderWrapper 
        { 
            get
            {
                return orderWrapper;
            }
            set
            {
                orderWrapper = value;
                if (orderWrapper != null)
                {
                    var decodedId = OrderIdConverter.Decode(orderWrapper.OrderId);
                    OrderSide = decodedId.Side;
                    Asset = decodedId.Asset;
                }
            }
        }

        public OrderSide OrderSide { get; private set; }

        public int Asset { get; private set; }
    }
}
