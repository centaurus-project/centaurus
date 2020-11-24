using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class OrderMap : IOrderMap
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

        public IEnumerable<Order> GetAllAccountOrders(Account account)
        {
            return map.Values.Where(o => o.Account == account);
        }

        public OrderInfo GetNextOrder(ulong currentOrderId)
        {
            var currentDecodedOrderId = OrderIdConverter.Decode(currentOrderId);

            var order = currentOrderId > 0 ? GetOrder(currentOrderId)?.Next : map.Values.FirstOrDefault();
            while (true)
            {
                if (order == null)
                    return null;

                var decodedOrderId = OrderIdConverter.Decode(order.OrderId);
                if (decodedOrderId.Side != currentDecodedOrderId.Side)
                {
                    order = order.Next;
                    continue;
                }

                return new OrderInfo
                {
                    OrderId = order.OrderId,
                    Side = decodedOrderId.Side,
                    Amount = order.Amount,
                    Price = order.Price,
                    Market = decodedOrderId.Asset
                };
            }
        }
    }
}
