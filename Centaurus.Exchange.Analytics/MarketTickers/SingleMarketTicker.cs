namespace Centaurus.Exchange.Analytics
{
    public class SingleMarketTicker
    {
        public SingleMarketTicker(int market)
        {
            Market = market;
        }

        public int Market { get; }

        public double Open { get; set; }

        public double Close { get; set; }

        public double High { get; set; }

        public double Low { get; set; }

        public double BaseAssetVolume { get; set; }

        public double MarketAssetVolume { get; set; }
    }
}