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
            lock (map)
                map.Clear();
        }

        public void AddOrder(OrderInfoWrapper orderWrapper)
        {
            lock (map)
                map.Add(orderWrapper.Order.OrderId, orderWrapper);
        }

        public void RemoveOrder(ulong orderId)
        {
            lock (map)
                map.Remove(orderId);
        }

        public OrderInfoWrapper GetOrder(ulong orderId)
        {
            lock (map)
            {
                if (!map.TryGetValue(orderId, out OrderInfoWrapper order)) 
                    return null;
                return order;
            }
        }

        public List<OrderInfoWrapper> All()
        {
            lock (map)
                return map.Values.OrderBy(o => o.Order.OrderId).ToList();
        }
    }
}
