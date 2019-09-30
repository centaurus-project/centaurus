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
        public List<Effect> ExecuteOrder(RequestQuantum orderRequestQuantum)
        {
            return new OrderMatcher(orderRequestQuantum.RequestEnvelope.Message as OrderRequest, orderRequestQuantum.Apex).Match();
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

        public Market AddMarket(AssetSettings asset)
        {
            var market = asset.CreateMarket();
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
            return GetMarket(parts.Asset).GetOrderbook(parts.Side);
        }
    }
}
