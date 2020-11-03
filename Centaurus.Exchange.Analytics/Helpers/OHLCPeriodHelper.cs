using Centaurus.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public static class OHLCPeriodHelper
    {
        public static DateTime Trim(this DateTime dateTime, OHLCFramePeriod period)
        {
            switch (period)
            {
                case OHLCFramePeriod.Minute:
                    return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, 0, dateTime.Kind);
                case OHLCFramePeriod.Hour:
                    return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, 0, 0, dateTime.Kind);
                case OHLCFramePeriod.Day:
                    return dateTime.Date;
                case OHLCFramePeriod.Week:
                    return GetPeriodMonday(dateTime);
                case OHLCFramePeriod.Month:
                    return GetPeriodMonthFirstDay(dateTime);
                default:
                    throw new InvalidOperationException($"{period} period is not supported.");
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
                case OHLCFramePeriod.Minute:
                    return (int)(dateTo - dateFrom).TotalMinutes;
                case OHLCFramePeriod.Hour:
                    return (int)(dateTo - dateFrom).TotalHours;
                case OHLCFramePeriod.Day:
                    return (int)(dateTo - dateFrom).TotalDays;
                case OHLCFramePeriod.Week:
                case OHLCFramePeriod.Month:
                    var totalDiff = 0;
                    while ((dateTo - dateFrom).TotalDays > 0)
                    {
                        dateFrom = period == OHLCFramePeriod.Week ? dateFrom.AddDays(7) : dateFrom.AddMonths(1);
                        totalDiff++;
                    }
                    return totalDiff;
                default:
                    throw new NotSupportedException($"{period} is not supported yet.");
            }
        }

        #region private members

        static DateTime GetPeriodMonday(DateTime dateTime)
        {
            return dateTime.AddDays((int)DayOfWeek.Monday - (dateTime.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)dateTime.DayOfWeek)).Date;
        }

        static DateTime GetPeriodMonthFirstDay(DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, 1, 0, 0, 0, dateTime.Kind).Date;
        }

        #endregion
    }
}
