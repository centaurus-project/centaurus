using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace Centaurus.Exchange.Analytics
{
    public class PriceHistoryFrame
    {
        public const int OpenValueIndex = 0;
        public const int HighValueIndex = 1;
        public const int LowValueIndex = 2;
        public const int CloseValueIndex = 3;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="startTime">Trimmed date time</param>
        /// <param name="period"></param>
        public PriceHistoryFrame(DateTime startTime, PriceHistoryPeriod period, int market, double open)
        {
            StartTime = UpdatedAt = startTime;
            Period = period;
            Market = market;
            Open = Close = open;
        }

        public DateTime StartTime { get; }

        public PriceHistoryPeriod Period { get; }

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

        public void OnTrade(Trade trade, DateTime updateDate)
        {
            if (trade == null)
                throw new ArgumentNullException(nameof(trade));
            UpdatedAt = updateDate;
            if (!HadTrades) //register first trade
            {
                Open = High = Low = Close = trade.Price;
                BaseVolume = trade.QuoteAmount;
                CounterVolume = trade.Amount;
                HadTrades = true;
                return;
            }
            if (High < trade.Price)
                High = trade.Price;
            if (Low > trade.Price)
                Low = trade.Price;
            Close = trade.Price;
            BaseVolume += trade.QuoteAmount;
            CounterVolume += trade.Amount;
        }

        public bool IsExpired(DateTime currentDateTime)
        {
            if (StartTime > currentDateTime)
                return false;
            return StartTime.GetDiff(currentDateTime, Period) != 0;
        }

        public DateTime UpdatedAt { get; private set; }

        public bool HadTrades { get; private set; }
    }
}