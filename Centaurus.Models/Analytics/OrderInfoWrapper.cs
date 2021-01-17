using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models.Analytics
{
    public class OrderInfoWrapper
    {
        public OrderInfo Order { get; set; }

        public OrderInfoWrapper Next { get; set; }

        public OrderInfoWrapper Prev { get; set; }
    }
}
