using System;

namespace Centaurus.Exchange.Analytics
{
    public class MarketTicker
    {
        public MarketTicker(int market)
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

        public DateTime UpdatedAt { get; set; }

        public override bool Equals(object obj)
        {
            return obj is MarketTicker ticker &&
                   Market == ticker.Market &&
                   Open == ticker.Open &&
                   Close == ticker.Close &&
                   High == ticker.High &&
                   Low == ticker.Low &&
                   BaseAssetVolume == ticker.BaseAssetVolume &&
                   MarketAssetVolume == ticker.MarketAssetVolume;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Market, Open, Close, High, Low, BaseAssetVolume, MarketAssetVolume);
        }
    }
}