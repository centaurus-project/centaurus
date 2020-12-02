using Centaurus.Models;

namespace Centaurus.Domain
{
    public class Market
    {
        public Market(int asset, OrderMap orderMap)
        {
            Asset = asset;
            Asks = new Orderbook(orderMap) { Side = OrderSide.Sell };
            Bids = new Orderbook(orderMap) { Side = OrderSide.Buy };
        }

        public int Asset { get; }

        public double LastPrice { get; set; }

        public Orderbook Asks { get; }

        public Orderbook Bids { get; }
    }
}
