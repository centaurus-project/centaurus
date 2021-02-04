using System;
using System.Text.Json.Serialization;

namespace Centaurus.Exchange.Analytics
{
    public class MarketTicker
    {
        public const int OpenValueIndex = 0;
        public const int HighValueIndex = 1;
        public const int LowValueIndex = 2;
        public const int CloseValueIndex = 3;

        public MarketTicker(int market)
        {
            Market = market;
        }

        public int Market { get; }

        public double[] OHLC { get; set; } = new double[4];

        [JsonIgnore]
        public double Open
        {
            get => OHLC[OpenValueIndex];
            set => OHLC[OpenValueIndex] = value;
        }


        [JsonIgnore]
        public double Close
        {
            get => OHLC[CloseValueIndex];
            set => OHLC[CloseValueIndex] = value;
        }


        [JsonIgnore]
        public double High
        {
            get => OHLC[HighValueIndex];
            set => OHLC[HighValueIndex] = value;
        }


        [JsonIgnore]
        public double Low
        {
            get => OHLC[LowValueIndex];
            set => OHLC[LowValueIndex] = value;
        }

        public double BaseVolume { get; set; }

        public double CounterVolume { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}