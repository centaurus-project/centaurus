using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class OHLCFrame
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="startTime">Trimmed date time</param>
        /// <param name="period"></param>
        public OHLCFrame(DateTime startTime, OHLCFramePeriod period, int market, double open)
        {
            StartTime = UpdatedAt = startTime;
            Period = period;
            Market = market;
            Open = Close = open;
        }

        public DateTime StartTime { get; }

        public OHLCFramePeriod Period { get; }

        public int Market { get; }

        public double High { get; set; }

        public double Low { get; set; }

        public double Open { get; set; }

        public double Close { get; set; }

        public double BaseAssetVolume { get; set; }

        public double MarketAssetVolume { get; set; }

        public void OnTrade(Trade trade)
        {
            if (trade == null)
                throw new ArgumentNullException(nameof(trade));
            UpdatedAt = DateTime.UtcNow;
            if (!HadTrades) //register first trade
            {
                Open = High = Low = Close = trade.Price;
                BaseAssetVolume = trade.BaseAmount;
                MarketAssetVolume = trade.Amount;
                HadTrades = true;
                return;
            }
            if (High < trade.Price)
                High = trade.Price;
            if (Low > trade.Price)
                Low = trade.Price;
            Close = trade.Price;
            BaseAssetVolume += trade.BaseAmount;
            MarketAssetVolume += trade.Amount;
        }

        public bool IsExpired(DateTime currentDateTime)
        {
            return StartTime.GetDiff(currentDateTime, Period) != 0;
        }

        public DateTime UpdatedAt { get; private set; }

        public bool HadTrades { get; private set; }
    }
}