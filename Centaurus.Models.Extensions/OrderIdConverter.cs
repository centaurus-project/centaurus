using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class OrderIdConverter
    {
        public struct DecodedOrder
        {
            public ulong Apex;
            public int Asset;
            public OrderSide Side;
        }

        public static ulong Encode(ulong apex, int market, OrderSide side)
        {
            return ((apex << 16) + (ulong)((market & 0x7FFF) << 1) + (ulong)(side == OrderSide.Sell ? 0 : 1));
        }

        public static DecodedOrder Decode(ulong orderId)
        {
            var decoded = new DecodedOrder
            {
                Apex = orderId >> 16,
                Asset = (int)((orderId >> 1) & 0x00007FFF),
                Side = orderId % 2 == 1 ? OrderSide.Buy : OrderSide.Sell
            };
            return decoded;
        }

        public static ulong FromRequest(OrderRequest request, long apex)
        {
            return Encode(unchecked((ulong)apex), request.Asset, request.Side);
        }
    }
}