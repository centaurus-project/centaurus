using Centaurus.Domain.Models;
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
        public List<OrderWrapper> sortedOrders = new List<OrderWrapper>();
        OrderComparer comparer;

        public OrderbookBinary(OrderMap orderMap, string market, OrderSide side)
            : base(orderMap, market, side)
        {
            comparer = new OrderComparer(side);
        }

        /// <summary>
        /// Add new order to the orderbook.
        /// </summary>
        /// <param name="order">An order to add</param>
        public override void InsertOrder(OrderWrapper order)
        {
            var i = ~sortedOrders.BinarySearch(order, comparer);
            var cursor = default(OrderWrapper);
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
        public override bool RemoveOrder(ulong orderId, out OrderWrapper order)
        {
            var isHead = orderId == Head?.Apex;
            if (!base.RemoveOrder(orderId, out order))
                return false;

            if (isHead)
                sortedOrders.RemoveAt(0);
            else
            {
                var index = sortedOrders.BinarySearch(order, comparer);
                sortedOrders.RemoveAt(index);
            }
            return true;
        }
    }

    public class OrderComparer : IComparer<OrderWrapper>
    {
        private OrderSide side;

        public OrderComparer(OrderSide side)
        {
            this.side = side;
        }

        public int Compare([NotNull] OrderWrapper x, [NotNull] OrderWrapper y)
        {
            var priceCompareRes = side == OrderSide.Sell ? x.Order.Price.CompareTo(y.Order.Price) : y.Order.Price.CompareTo(x.Order.Price);
            if (priceCompareRes == 0)
                return x.Apex.CompareTo(y.Apex);
            return priceCompareRes;
        }
    }
}