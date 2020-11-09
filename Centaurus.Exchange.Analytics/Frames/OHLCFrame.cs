using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public class OHLCFrame : IEquatable<OHLCFrame>
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

        public void OnTrade(Trade trade)
        {
            if (trade == null)
                throw new ArgumentNullException(nameof(trade));
            if (Open == default) //register first trade
            {
                Open = Hi = Low = Close = trade.Price;
                Volume = trade.BaseAmount;
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

        public override bool Equals(object obj)
        {
            return Equals(obj as OHLCFrame);
        }

        public bool Equals(OHLCFrame other)
        {
            return other != null &&
                   StartTime == other.StartTime &&
                   Period == other.Period &&
                   Market == other.Market &&
                   Hi == other.Hi &&
                   Low == other.Low &&
                   Open == other.Open &&
                   Close == other.Close &&
                   Volume == other.Volume;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StartTime, Period, Market, Hi, Low, Open, Close, Volume);
        }

        public static bool operator ==(OHLCFrame left, OHLCFrame right)
        {
            return EqualityComparer<OHLCFrame>.Default.Equals(left, right);
        }

        public static bool operator !=(OHLCFrame left, OHLCFrame right)
        {
            return !(left == right);
        }
    }
}