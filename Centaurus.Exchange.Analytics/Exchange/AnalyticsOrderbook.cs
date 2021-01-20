using Centaurus.Models;
using Centaurus.Models.Analytics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class AnalyticsOrderbook : IEnumerable// : IXdrSerializableModel
    {
        private AnalyticsOrderMap orderMap;

        public AnalyticsOrderbook(AnalyticsOrderMap orderMap)
        {
            this.orderMap = orderMap;
        }

        public OrderSide Side { get; set; }

        public OrderInfoWrapper Head { get; set; }

        public OrderInfoWrapper Tail { get; set; }

        public int Count { get; set; }

        public long TotalAmount { get; set; }

        public long Volume { get; set; }

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
            var price = order.Order.Price;
            //just set Head reference if it's the first order
            if (Head == null)
            {
                InsertOrderBefore(order, null);
                return;
            }
            //find position to insert the order
            var cursor = Head;
            while ((Side == OrderSide.Sell && price >= cursor.Order.Price)
                || (Side == OrderSide.Buy && price <= cursor.Order.Price))
            {
                cursor = cursor.Next;
                if (cursor == null) break; //the last record
            }

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
            TotalAmount += orderWrapper.Order.Amount;
            Volume += (long)(orderWrapper.Order.Amount * orderWrapper.Order.Price);
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
            var enumerator = GetEnumerator();

            while (enumerator.MoveNext())
            {
                var currentOrder = enumerator.Current;
                if (enumerator.Current.Order.OrderId == orderId)
                {
                    if (Head == currentOrder)
                        Head = currentOrder.Next;
                    if (Tail == currentOrder)
                        Tail = currentOrder.Prev;
                    if (currentOrder.Prev != null)
                        currentOrder.Prev.Next = currentOrder.Next;
                    if (currentOrder.Next != null)
                        currentOrder.Next.Prev = currentOrder.Prev;

                    orderMap.RemoveOrder(orderId);
                    return true;
                }
            }
            return false;
        }

        public OrderInfoWrapper GetOrder(ulong orderId)
        {
            return orderMap.GetOrder(orderId);
        }
    }
}
