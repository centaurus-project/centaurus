using Centaurus.Models;

namespace Centaurus.Domain
{
    public class ExchangeMarket
    {
        public ExchangeMarket(int market, OrderMap orderMap)
        {
            Market = market;
            Asks = new Orderbook(orderMap, market, OrderSide.Sell);
            Bids = new Orderbook(orderMap, market, OrderSide.Buy);
        }

        public int Market { get; }

        public double LastPrice { get; set; }

        public Orderbook Asks { get; }

        public Orderbook Bids { get; }
    }
}
