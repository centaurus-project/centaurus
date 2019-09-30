using Centaurus.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class Orderbook: IEnumerable// : IXdrSerializableModel
    {
        public OrderSides Side { get; set; }

        public Order Head { get; set; }

        public Order Tail { get; set; }

        public int Count { get; set; }

        public long TotalAmount { get; set; }

        public long Volume { get; set; }

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
            while ((Side == OrderSides.Sell && price >= cursor.Price)
                || (Side == OrderSides.Buy && price <= cursor.Price))
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
        public void InsertOrderBefore(Order order, Order before)
        {
            if (before == null)
            {//append to the end
                if (Tail != null)
                { //insert after the tail
                    Tail.Next = order;
                    order.Prev = Tail;
                    Tail = order;
                }
                else
                { //it's the first order entry
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
            }
            //increment count
            Count++;
            TotalAmount += order.Amount;
            Volume += (long)(order.Amount * order.Price);
            //add to the map
            Global.Exchange.OrderMap.AddOrder(order);
        }
    }
}
