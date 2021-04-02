using Centaurus.Models;

namespace Centaurus.Domain
{
    public class ExchangeMarket
    {
        public ExchangeMarket(int market, OrderMap orderMap, bool useLegacyOrderbook = false)
        {
            Market = market;
            Asks = useLegacyOrderbook 
                ? (OrderbookBase)new Orderbook(orderMap, market, OrderSide.Sell)
                : new OrderbookBinary(orderMap, market, OrderSide.Sell);
            Bids = useLegacyOrderbook
                ? (OrderbookBase)new Orderbook(orderMap, market, OrderSide.Buy)
                : new OrderbookBinary(orderMap, market, OrderSide.Buy);
        }

        public int Market { get; }

        public double LastPrice { get; set; }

        public OrderbookBase Asks { get; }

        public OrderbookBase Bids { get; }
    }
}
