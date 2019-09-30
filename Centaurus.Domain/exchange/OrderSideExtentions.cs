using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class OrderSideExtentions
    {
        public static OrderSides Inverse(this OrderSides side)
        {
            return side == OrderSides.Buy ? OrderSides.Sell : OrderSides.Buy;
        }
    }
}
