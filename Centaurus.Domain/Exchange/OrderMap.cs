using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
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

        public bool RemoveOrder(ulong orderId, out Order order)
        {
            return map.Remove(orderId, out order);
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

        public IEnumerable<Order> GetAllAccountOrders(AccountWrapper account)
        {
            return map.Values.Where(o => o.AccountWrapper == account);
        }
    }
}
