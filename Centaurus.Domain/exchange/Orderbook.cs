using Centaurus.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class Orderbook : IEnumerable// : IXdrSerializableModel
    {
        private OrderMap orderMap;

        public Orderbook(OrderMap orderMap)
        {
            this.orderMap = orderMap;
        }

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
            orderMap.AddOrder(order);
        }


        /// <summary>
        /// Remove the first executed order from the orderbook.
        /// </summary>
        public void RemoveEmptyHeadOrder()
        {
            var currentHead = Head;
            if (currentHead.Amount > 0L) throw new Exception($"Requested order removal for the non-empty order {Head.OrderId}.");
            var newHead = currentHead.Next;
            if (newHead != null)
            {
                newHead.Prev = null;
                Head = newHead;
            }
            else
            {
                Head = Tail = null;
            }
            Count--;
            orderMap.RemoveOrder(currentHead.OrderId);
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
            var enumerator = GetEnumerator();

            while (enumerator.MoveNext())
            {
                var currentOrder = enumerator.Current;
                if (enumerator.Current.OrderId == orderId)
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

        public Order GetOrder(ulong orderId)
        {
            return orderMap.GetOrder(orderId);
        }
    }
}
