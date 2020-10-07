using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public class Exchange
    {
        private Dictionary<int, Market> Markets = new Dictionary<int, Market>();

        public OrderMap OrderMap { get; } = new OrderMap();

        /// <summary>
        /// Process customer's order request.
        /// </summary>
        /// <param name="orderRequest">Order request quantum</param>
        /// <returns></returns>
        public void ExecuteOrder(EffectProcessorsContainer effectsContainer)
        {
            RequestQuantum orderRequestQuantum = (RequestQuantum)effectsContainer.Envelope.Message;
            var orderRequest = (OrderRequest)orderRequestQuantum.RequestEnvelope.Message;
            new OrderMatcher(orderRequest, effectsContainer).Match();
        }

        internal IEnumerable<Market> GetAllMarkets()
        {
            return Markets.Values;
        }

        public Market GetMarket(int asset)
        {
            if (Markets.TryGetValue(asset, out Market market)) return market;
            throw new InvalidOperationException($"Asset {asset} is not supported");
        }

        public bool HasMarket(int asset)
        {
            return Markets.ContainsKey(asset);
        }

        public Market AddMarket(AssetSettings asset)
        {
            var market = asset.CreateMarket(OrderMap);
            Markets.Add(asset.Id, market);
            return market;
        }

        public void Clear()
        {
            Markets.Clear();
            OrderMap.Clear();
        }

        public Orderbook GetOrderbook(ulong offerId)
        {
            var parts = OrderIdConverter.Decode(offerId);
            return GetOrderbook(parts.Asset, parts.Side);
        }

        public Orderbook GetOrderbook(int asset, OrderSides side)
        {
            return GetMarket(asset).GetOrderbook(side);
        }

        public Order GetOrder(ulong offerId)
        {
            return GetOrderbook(offerId).GetOrder(offerId);
        }

        public bool RemoveOrder(ulong offerId)
        {
            return GetOrderbook(offerId).RemoveOrder(offerId);
        }

        public static Exchange RestoreExchange(List<AssetSettings> assets, List<Order> orders)
        {
            var exchange = new Exchange();
            foreach (var asset in assets)
                exchange.AddMarket(asset);

            foreach (var order in orders)
            {
                var orderData = OrderIdConverter.Decode(order.OrderId);
                var market = exchange.GetMarket(orderData.Asset);
                var orderbook = market.GetOrderbook(orderData.Side);
                orderbook.InsertOrder(order);
            }
            return exchange;
        }
    }
}
