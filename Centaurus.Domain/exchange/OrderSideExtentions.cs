using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class OrderSideExtentions
    {
        public static OrderSide Inverse(this OrderSide side)
        {
            return side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        }
    }
}
