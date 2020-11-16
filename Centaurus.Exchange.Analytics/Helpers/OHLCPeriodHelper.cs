using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public static class OHLCPeriodHelper
    {
        const long TicksPerWeek = TimeSpan.TicksPerDay * 7;

        public static long TicksPerPeriod(OHLCFramePeriod period)
        {
                switch (period)
                {
                    case OHLCFramePeriod.Minute:
                        return TimeSpan.TicksPerMinute;
                    case OHLCFramePeriod.Minutes15:
                        return TimeSpan.TicksPerMinute * 15;
                    case OHLCFramePeriod.Minutes30:
                        return TimeSpan.TicksPerMinute * 30;
                    case OHLCFramePeriod.Hour:
                        return TimeSpan.TicksPerHour;
                    case OHLCFramePeriod.Hours4:
                        return TimeSpan.TicksPerHour * 4;
                    case OHLCFramePeriod.Day:
                        return TimeSpan.TicksPerDay;
                    case OHLCFramePeriod.Week:
                        return TicksPerWeek;
                    default:
                        throw new InvalidOperationException($"{period} doesn't support ticks.");
                }
        }

        public static DateTime Trim(this DateTime dateTime, OHLCFramePeriod period)
        {
            switch (period)
            {
                case OHLCFramePeriod.Month:
                    return new DateTime(dateTime.Year, dateTime.Month, 1, 0, 0, 0, dateTime.Kind).Date;
                default:
                    var trimmed = dateTime.Ticks - dateTime.Ticks % TicksPerPeriod(period);
                    return new DateTime(trimmed, dateTime.Kind);
            }
        }

        public static DateTime GetNextFrameDate(this DateTime dateTime, OHLCFramePeriod period)
        {
            switch (period)
            {
                case OHLCFramePeriod.Month:
                    return dateTime.AddMonths(1);
                default:
                    return dateTime.AddTicks(TicksPerPeriod(period));
            }
        }

        /// <summary>
        /// Get diff between two dates in specified periods. DateTime values must be already trimmed 
        /// </summary>
        /// <param name="dateFrom"></param>
        /// <param name="periodDateTime"></param>
        /// <param name="period"></param>
        /// <returns></returns>
        public static int GetDiff(this DateTime dateFrom, DateTime dateTo, OHLCFramePeriod period)
        {
            if (dateFrom > dateTo)
            {
                throw new InvalidOperationException("Date from is greater than date to.");
            }

            switch (period)
            {
                case OHLCFramePeriod.Month:
                    var totalDiff = 0;
                    while ((dateTo - dateFrom).TotalDays > 0)
                    {
                        dateFrom = dateFrom.AddMonths(1);
                        totalDiff++;
                    }
                    return totalDiff;
                default:
                    return (int)Math.Floor(decimal.Divide(dateTo.Ticks - dateFrom.Ticks, TicksPerPeriod(period)));
            }
        }
    }
}
