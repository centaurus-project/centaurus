using Centaurus.Domain.Models;
using Centaurus.Models;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class OrderbookBase : IEnumerable
    {
        private OrderMap orderMap;

        public OrderbookBase(OrderMap orderMap, string market, OrderSide side)
        {
            this.orderMap = orderMap;
            Side = side;
            Market = market;
        }

        public OrderSide Side { get; }

        public string Market { get; }
        public OrderWrapper Head { get; set; }

        public OrderWrapper Tail { get; set; }

        public int Count { get; set; }

        public IEnumerator<OrderWrapper> GetEnumerator()
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

        public abstract void InsertOrder(OrderWrapper order);

        /// <summary>
        /// Insert order before the specific offer.
        /// </summary>
        /// <param name="orderbook">Orderbook</param>
        /// <param name="order">An order to insert</param>
        /// <param name="before"></param>
        protected void InsertOrderBefore(OrderWrapper order, OrderWrapper before)
        {
            if (before == null)
            {
                //append to the end
                if (Tail != null)
                {
                    //insert after the tail
                    Tail.Next = order;
                    order.Prev = Tail;
                    Tail = order;
                }
                else
                {
                    //it's the first order entry
                    Head = Tail = order;
                }
            }
            else
            {
                //insert order into the linked list before the cursor
                order.Prev = before.Prev;
                order.Next = before;
                before.Prev = order;
                //update the reference if we are inserting before the head entry
                if (before == Head)
                {
                    Head = order;
                }
                else
                {
                    order.Prev.Next = order;
                }
            }
            //increment count
            Count++;
            //add to the map
            orderMap.AddOrder(order);
        }

        /// <summary>
        /// Get orderbook best price.
        /// </summary>
        /// <returns>Best price.</returns>
        public double GetBestPrice()
        {
            return Head?.Order.Price ?? .0;
        }

        /// <summary>
        /// Removes order by id
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns>Removal result.</returns>
        public virtual bool RemoveOrder(ulong orderId, out OrderWrapper order)
        {
            if (!orderMap.RemoveOrder(orderId, out order))
                return false;
            var next = order.Next;
            var prev = order.Prev;
            if (Head == order)
                Head = next;
            if (Tail == order)
                Tail = prev;
            if (prev != null)
                prev.Next = next;
            if (next != null)
                next.Prev = prev;

            Count--;
            return true;
        }

        public OrderWrapper GetOrder(ulong orderId)
        {
            return orderMap.GetOrder(orderId);
        }
    }
}