using Centaurus.Models;
using System;

namespace Centaurus.Domain
{
    public class ExchangeMarket
    {
        public ExchangeMarket(string market, OrderMap orderMap)
        {
            Market = market ?? throw new ArgumentNullException(nameof(market));
            Asks = new Orderbook(orderMap, market, OrderSide.Sell);
            Bids = new Orderbook(orderMap, market, OrderSide.Buy);
        }

        public string Market { get; }

        public double LastPrice { get; set; }
        
        public Orderbook Asks { get; }

        public Orderbook Bids { get; }
    }
}
