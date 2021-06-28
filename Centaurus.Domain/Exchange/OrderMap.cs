using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderMap
    {

        private Dictionary<ulong, OrderWrapper> map = new Dictionary<ulong, OrderWrapper>();

        public void Clear()
        {
            map.Clear();
        }

        public void AddOrder(OrderWrapper order)
        {
            map.Add(order.OrderId, order);
        }

        public bool RemoveOrder(ulong orderId, out OrderWrapper order)
        {
            return map.Remove(orderId, out order);
        }

        public OrderWrapper GetOrder(ulong orderId)
        {
            if (!map.TryGetValue(orderId, out OrderWrapper order)) return null;
            return order;
        }

        public IEnumerable<OrderWrapper> GetAllOrders()
        {
            return map.Values;
        }

        public IEnumerable<OrderWrapper> GetAllAccountOrders(AccountWrapper account)
        {
            return map.Values.Where(o => o.AccountWrapper == account);
        }
    }
}
