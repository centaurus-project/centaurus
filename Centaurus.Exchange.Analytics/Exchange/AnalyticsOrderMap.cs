using Centaurus.Models;
using Centaurus.Models.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class AnalyticsOrderMap
    {
        private Dictionary<ulong, OrderInfoWrapper> map = new Dictionary<ulong, OrderInfoWrapper>();

        public void Clear()
        {
            lock (this)
                map.Clear();
        }

        public void AddOrder(OrderInfoWrapper orderWrapper)
        {
            lock (this)
                map.Add(orderWrapper.Order.OrderId, orderWrapper);
        }

        public void RemoveOrder(ulong orderId)
        {
            lock (this)
                map.Remove(orderId);
        }

        public OrderInfoWrapper GetOrder(ulong orderId)
        {
            lock (this)
            {
                if (!map.TryGetValue(orderId, out OrderInfoWrapper order)) return null;
                return order;
            }
        }

        /// <summary> 
        /// </summary>
        /// <param name="currentOrderId">If equal to default, first order will be returned.</param>
        /// <returns>Next order</returns>
        public OrderInfo GetNextOrder(ulong currentOrderId)
        {
            lock (this)
            {
                var currentDecodedOrderId = OrderIdConverter.Decode(currentOrderId);
                var orderWrapper = currentOrderId > 0 ? GetOrder(currentOrderId)?.Next : map.Values.FirstOrDefault();
                while (true)
                {
                    if (orderWrapper == null)
                        return null;

                    var decodedOrderId = OrderIdConverter.Decode(orderWrapper.Order.OrderId);
                    if (decodedOrderId.Side != currentDecodedOrderId.Side)
                    {
                        orderWrapper = orderWrapper.Next;
                        continue;
                    }

                    return orderWrapper.Order;
                }
            }
        }
    }
}
