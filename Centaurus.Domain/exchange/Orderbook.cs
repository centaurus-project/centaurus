using Centaurus.Models;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Centaurus.Domain
{
    public class Orderbook : IEnumerable// : IXdrSerializableModel
    {
        private OrderMap orderMap;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Orderbook(OrderMap orderMap)
        {
            this.orderMap = orderMap;
        }

        public OrderSide Side { get; set; }

        public Order Head { get; set; }

        public Order Tail { get; set; }

        public int Count { get; set; }

        public IEnumerator<Order> GetEnumerator()
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
        public void InsertOrder(Order order)
        {
            var price = order.Price;
            //just set Head reference if it's the first order
            if (Head == null)
            {
                InsertOrderBefore(order, null);
                return;
            }
            //find position to insert the order
            var cursor = Head;
            while ((Side == OrderSide.Sell && price >= cursor.Price)
                || (Side == OrderSide.Buy && price <= cursor.Price))
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
        /// <param name="order">An order to insert</param>
        /// <param name="before"></param>
        private void InsertOrderBefore(Order order, Order before)
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
            return Head?.Price ?? .0;
        }

        /// <summary>
        /// Removes order by id
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns>Removal result.</returns>
        public bool RemoveOrder(ulong orderId)
        {
            var order = GetOrder(orderId);
            if (order == null)
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

            orderMap.RemoveOrder(orderId);
            Count--;
            return true;
        }

        public Order GetOrder(ulong orderId)
        {
            return orderMap.GetOrder(orderId);
        }
    }
}