using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class MarketExtensions
    {
        public static Orderbook GetOrderbook(this Market market, OrderSides side)
        {
            return side == OrderSides.Buy ? market.Bids : market.Asks;
        }
    }
}