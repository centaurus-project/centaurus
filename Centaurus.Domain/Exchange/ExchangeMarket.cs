using Centaurus.Models;
using System;

namespace Centaurus.Domain
{
    public class ExchangeMarket
    {
        public ExchangeMarket(string market, OrderMap orderMap, bool useLegacyOrderbook = false)
        {
            Market = market ?? throw new ArgumentNullException(nameof(market));
            Asks = useLegacyOrderbook 
                ? (OrderbookBase)new Orderbook(orderMap, market, OrderSide.Sell)
                : new OrderbookBinary(orderMap, market, OrderSide.Sell);
            Bids = useLegacyOrderbook
                ? (OrderbookBase)new Orderbook(orderMap, market, OrderSide.Buy)
                : new OrderbookBinary(orderMap, market, OrderSide.Buy);
        }

        public string Market { get; }

        public double LastPrice { get; set; }
        
        public OrderbookBase Asks { get; }

        public OrderbookBase Bids { get; }
    }
}
