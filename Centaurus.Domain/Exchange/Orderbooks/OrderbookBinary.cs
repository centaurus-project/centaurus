using Centaurus.Models;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderbookBinary : OrderbookBase
    {
        public List<Order> sortedOrders = new List<Order>();
        OrderComparer comparer;

        public OrderbookBinary(OrderMap orderMap, int market, OrderSide side)
            : base(orderMap, market, side)
        {
            comparer = new OrderComparer(side);
        }

        /// <summary>
        /// Add new order to the orderbook.
        /// </summary>
        /// <param name="order">An order to add</param>
        public override void InsertOrder(Order order)
        {
            var i = ~sortedOrders.BinarySearch(order, comparer);
            var cursor = default(Order);
            if (i == 0)
                cursor = sortedOrders.FirstOrDefault();
            else if (sortedOrders.Count > i)
                cursor = sortedOrders[i];
            sortedOrders.Insert(i, order);

            //insert order
            InsertOrderBefore(order, cursor);
        }

        /// <summary>
        /// Removes order by id
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns>Removal result.</returns>
        public override bool RemoveOrder(ulong orderId, out Order order)
        {
            if (!base.RemoveOrder(orderId, out order))
                return false;

            var index = sortedOrders.BinarySearch(order, comparer);
            sortedOrders.RemoveAt(index);
            return true;
        }
    }

    public class OrderComparer : IComparer<Order>
    {
        private OrderSide side;

        public OrderComparer(OrderSide side)
        {
            this.side = side;
        }

        public int Compare([NotNull] Order x, [NotNull] Order y)
        {
            var priceCompareRes = side == OrderSide.Sell ? x.Price.CompareTo(y.Price) : y.Price.CompareTo(x.Price);
            if (priceCompareRes == 0)
                return x.OrderId.CompareTo(y.OrderId);
            return priceCompareRes;
        }
    }
}