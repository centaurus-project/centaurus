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
        public OHLCFrame(DateTime startTime, OHLCFramePeriod period, int market)
        {
            StartTime = startTime;
            Period = period;
            Market = market;
        }

        public DateTime StartTime { get; }

        public OHLCFramePeriod Period { get; }

        public int Market { get; }

        public double Hi { get; set; }

        public double Low { get; set; }

        public double Open { get; set; }

        public double Close { get; set; }

        public double Volume { get; set; }

        public void RegisterTrade(Trade trade)
        {
            if (trade == null)
                throw new ArgumentNullException(nameof(trade));
            if (Open == default) //register first trade
            {
                Open = Hi = Low = Close;
                return;
            }
            if (Hi < trade.Price)
                Hi = trade.Price;
            if (Low > trade.Price)
                Low = trade.Price;
            Close = trade.Price;
            Volume += trade.BaseAmount;
        }

        public bool IsExpired(DateTime currentDateTime)
        {
            return StartTime.GetDiff(currentDateTime, Period) != 0;
        }
    }
}