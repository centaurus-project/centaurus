using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Analytics
{
    public class OrderUpdate
    {
        public OrderUpdate(OrderInfo order, bool isDeleted = false)
        {
            Order = order;
            IsDeleted = isDeleted;
        }

        public OrderInfo Order { get; }

        public bool IsDeleted { get; }
    }
}
