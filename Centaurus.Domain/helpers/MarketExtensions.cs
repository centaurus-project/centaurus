using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class MarketExtensions
    {
        public static Orderbook GetOrderbook(this Market market, OrderSide side)
        {
            return side == OrderSide.Buy ? market.Bids : market.Asks;
        }
    }
}