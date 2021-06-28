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
    public class Orderbook : OrderbookBase
    {
        public Orderbook(OrderMap orderMap, int market, OrderSide side)
            : base(orderMap, market, side)
        {
        }

        /// <summary>
        /// Add new order to the orderbook.
        /// </summary>
        /// <param name="order">An order to add</param>
        public override void InsertOrder(OrderWrapper order)
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
    }
}