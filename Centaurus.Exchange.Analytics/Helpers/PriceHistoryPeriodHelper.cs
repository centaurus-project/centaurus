using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public static class PriceHistoryPeriodHelper
    {
        const long TicksPerWeek = TimeSpan.TicksPerDay * 7;

        public static long TicksPerPeriod(PriceHistoryPeriod period)
        {
                switch (period)
                {
                    case PriceHistoryPeriod.Minutes15:
                        return TimeSpan.TicksPerMinute * 15;
                    case PriceHistoryPeriod.Minutes30:
                        return TimeSpan.TicksPerMinute * 30;
                    case PriceHistoryPeriod.Hour:
                        return TimeSpan.TicksPerHour;
                    case PriceHistoryPeriod.Hours4:
                        return TimeSpan.TicksPerHour * 4;
                    case PriceHistoryPeriod.Day:
                        return TimeSpan.TicksPerDay;
                    case PriceHistoryPeriod.Week:
                        return TicksPerWeek;
                    default:
                        throw new InvalidOperationException($"{period} doesn't support ticks.");
                }
        }

        public static DateTime Trim(this DateTime dateTime, PriceHistoryPeriod period)
        {
            switch (period)
            {
                case PriceHistoryPeriod.Month:
                    return new DateTime(dateTime.Year, dateTime.Month, 1, 0, 0, 0, dateTime.Kind).Date;
                default:
                    var trimmed = dateTime.Ticks - dateTime.Ticks % TicksPerPeriod(period);
                    return new DateTime(trimmed, dateTime.Kind);
            }
        }

        public static DateTime GetNextFrameDate(this DateTime dateTime, PriceHistoryPeriod period)
        {
            switch (period)
            {
                case PriceHistoryPeriod.Month:
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
        public static int GetDiff(this DateTime dateFrom, DateTime dateTo, PriceHistoryPeriod period)
        {
            if (dateFrom > dateTo)
            {
                throw new InvalidOperationException("Date from is greater than date to.");
            }

            switch (period)
            {
                case PriceHistoryPeriod.Month:
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
