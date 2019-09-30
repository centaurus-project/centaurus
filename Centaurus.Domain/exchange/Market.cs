using Centaurus.Models;

namespace Centaurus.Domain
{
    public class Market
    {
        public Market(int asset)
        {
            Asset = asset;
            Asks = new Orderbook() { Side = OrderSides.Sell };
            Bids = new Orderbook() { Side = OrderSides.Buy };
        }

        public int Asset { get; }

        public double LastPrice { get; set; }

        public Orderbook Asks { get; }

        public Orderbook Bids { get; }
    }
}
