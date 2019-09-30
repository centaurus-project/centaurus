using Centaurus.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public static class OrderbookExtensions
    {
        /// <summary>
        /// Remove the first executed order from the orderbook.
        /// </summary>
        public static void RemoveEmptyHeadOrder(this Orderbook orderbook)
        {
            var currentHead = orderbook.Head;
            if (currentHead.Amount > 0L) throw new Exception($"Requested order removal for the non-empty order {orderbook.Head.OrderId}.");
            var newHead = currentHead.Next;
            if (newHead != null)
            {
                newHead.Prev = null;
                orderbook.Head = newHead;
            }
            else
            {
                orderbook.Head = orderbook.Tail = null;
            }
            orderbook.Count--;
            Global.Exchange.OrderMap.RemoveOrder(currentHead.OrderId);
        }

        /// <summary>
        /// Get orderbook best price.
        /// </summary>
        /// <param name="orderbook">Orderbook</param>
        /// <returns>Best price.</returns>
        public static double GetBestPrice(this Orderbook orderbook)
        {
            return orderbook.Head?.Price ?? .0;
        }
    }
}
