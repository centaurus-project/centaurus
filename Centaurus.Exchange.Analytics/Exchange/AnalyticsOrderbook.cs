using Centaurus.Models;
using Centaurus.Models.Analytics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class AnalyticsOrderbook : IEnumerable// : IXdrSerializableModel
    {
        private AnalyticsOrderMap orderMap;

        public List<OrderInfoWrapper> sortedOrders = new List<OrderInfoWrapper>();
        OrderInfoWrapperComparer comparer;

        public AnalyticsOrderbook(AnalyticsOrderMap orderMap, OrderSide side)
        {
            this.orderMap = orderMap;
            Side = side;
            comparer = new OrderInfoWrapperComparer(Side);
        }

        public OrderSide Side { get; set; }

        public OrderInfoWrapper Head { get; set; }

        public OrderInfoWrapper Tail { get; set; }

        public int Count { get; set; }

        public ulong TotalAmount { get; set; }

        public ulong Volume { get; set; }

        public IEnumerator<OrderInfoWrapper> GetEnumerator()
        {
            var cursor = Head;
            while (cursor != null)
            {
                yield return cursor;
                cursor = cursor.Next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Add new order to the orderbook.
        /// </summary>
        /// <param name="order">An order to add</param>
        public void InsertOrder(OrderInfoWrapper order)
        {

            var i = ~sortedOrders.BinarySearch(order, comparer);
            var cursor = default(OrderInfoWrapper);
            if (i == 0)
                cursor = sortedOrders.FirstOrDefault();
            else if (sortedOrders.Count > i)
                cursor = sortedOrders[i];
            sortedOrders.Insert(i, order);

            //insert order
            InsertOrderBefore(order, cursor);
        }

        /// <summary>
        /// Insert order before the specific offer.
        /// </summary>
        /// <param name="orderbook">Orderbook</param>
        /// <param name="orderWrapper">An order to insert</param>
        /// <param name="beforeWrapper"></param>
        public void InsertOrderBefore(OrderInfoWrapper orderWrapper, OrderInfoWrapper beforeWrapper)
        {
            if (beforeWrapper == null)
            {//append to the end
                if (Tail != null)
                { //insert after the tail
                    Tail.Next = orderWrapper;
                    orderWrapper.Prev = Tail;
                    Tail = orderWrapper;
                }
                else
                { //it's the first order entry
                    Head = Tail = orderWrapper;
                }
            }
            else
            {
                //insert order into the linked list before the cursor
                orderWrapper.Prev = beforeWrapper.Prev;
                orderWrapper.Next = beforeWrapper;
                beforeWrapper.Prev = orderWrapper;
                //update the reference if we are inserting before the head entry
                if (beforeWrapper == Head)
                {
                    Head = orderWrapper;
                }
            }
            //increment count
            Count++;
            TotalAmount += orderWrapper.Order.AmountDiff;
            Volume += (ulong)(orderWrapper.Order.AmountDiff * orderWrapper.Order.Price);
            //add to the map
            orderMap.AddOrder(orderWrapper);
        }

        /// <summary>
        /// Removes order by id
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns>Removal result.</returns>
        public bool RemoveOrder(ulong orderId)
        {
            var order = orderMap.GetOrder(orderId);
            if (order == null)
                return false;
            if (Head == order)
                Head = order.Next;
            if (Tail == order)
                Tail = order.Prev;
            if (order.Prev != null)
                order.Prev.Next = order.Next;
            if (order.Next != null)
                order.Next.Prev = order.Prev;

            orderMap.RemoveOrder(orderId);

            var index = sortedOrders.BinarySearch(order, comparer);
            sortedOrders.RemoveAt(index);

            return true;
        }

        public OrderInfoWrapper GetOrder(ulong orderId)
        {
            return orderMap.GetOrder(orderId);
        }
    }
    public class OrderInfoWrapperComparer : IComparer<OrderInfoWrapper>
    {
        private OrderSide side;

        public OrderInfoWrapperComparer(OrderSide side)
        {
            this.side = side;
        }

        public int Compare([NotNull] OrderInfoWrapper x, [NotNull] OrderInfoWrapper y)
        {
            if (x == null)
                throw new ArgumentNullException(nameof(x));
            if (y == null)
                throw new ArgumentNullException(nameof(y));

            var priceCompareRes = side == OrderSide.Sell ? x.Order.Price.CompareTo(y.Order.Price) : y.Order.Price.CompareTo(x.Order.Price);
            if (priceCompareRes == 0)
                return x.Order.OrderId.CompareTo(y.Order.OrderId);
            return priceCompareRes;
        }
    }
}
