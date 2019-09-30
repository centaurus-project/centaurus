using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderIdConverter
    {
        public struct DecodedOrder
        {
            public ulong Apex;
            public int Asset;
            public OrderSides Side;
        }

        public static ulong Encode(ulong apex, int market, OrderSides side)
        {
            return ((apex << 16) + (ulong)((market & 0x7FFF) << 1) + (ulong)(side == OrderSides.Sell ? 0 : 1));
        }

        public static DecodedOrder Decode(ulong orderId)
        {
            var decoded = new DecodedOrder
            {
                Apex = orderId >> 16,
                Asset = (int)((orderId >> 1) & 0x00007FFF),
                Side = orderId % 2 == 1 ? OrderSides.Buy : OrderSides.Sell
            };
            return decoded;
        }
    }
}