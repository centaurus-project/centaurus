using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class OrderMap
    {
        private Dictionary<ulong, Order> map = new Dictionary<ulong, Order>();

        public void Clear()
        {
            map.Clear();
        }

        public void AddOrder(Order order)
        {
            map.Add(order.OrderId, order);
        }

        public void RemoveOrder(ulong orderId)
        {
            map.Remove(orderId);
        }

        public Order GetOrder(ulong orderId)
        {
            if (!map.TryGetValue(orderId, out Order order)) return null;
            return order;
        }

        public IEnumerable<Order> GetAllOrders()
        {
            return map.Values;
        }
    }
}
