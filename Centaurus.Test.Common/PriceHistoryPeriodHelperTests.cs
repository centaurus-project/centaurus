using Centaurus.Models;
using Centaurus.Exchange.Analytics;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Test
{
    public class PriceHistoryPeriodHelperTests
    {

        private DateTime GetMinDateForPeriod(PriceHistoryPeriod period)
        {
            var date = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            if (period == PriceHistoryPeriod.Month)
                return date;
            while (true)
            {
                date = date.AddTicks(PriceHistoryPeriodHelper.TicksPerPeriod(PriceHistoryPeriod.Minutes15));
                if (date.Ticks % PriceHistoryPeriodHelper.TicksPerPeriod(period) == 0)
                    return date;
            }
        }

        [Test]
        public void TrimTest()
        {
            var r = new Random();
            var periods = Enum.GetValues(typeof(PriceHistoryPeriod)).Cast<PriceHistoryPeriod>();
            foreach (var p in periods)
            {
                var minDate = GetMinDateForPeriod(p);
                for (var i = 0; i < 5; i++)
                {
                    var dateTime = minDate
                        .AddDays(r.Next(0, 1000))
                        .AddHours(r.Next(0, 24))
                        .AddMinutes(r.Next(0, 60))
                        .AddSeconds(r.Next(0, 60))
                        .AddMilliseconds(r.Next(0, 1000));
                    var trimmedDate = dateTime.Trim(p);
                    if (p == PriceHistoryPeriod.Month)
                    {
                        var date = default(DateTime);
                        while (date < trimmedDate)
                        {
                            date = date.AddMonths(1);
                        }
                        if (date == trimmedDate
                            && date.Day == 1
                            && date.Hour == 0
                            && date.Minute == 0
                            && date.Second == 0
                            && date.Millisecond == 0)
                            continue;
                    }
                    else
                    {
                        var date = minDate.Ticks;
                        while (date < trimmedDate.Ticks)
                            date += PriceHistoryPeriodHelper.TicksPerPeriod(p);
                        if (date == trimmedDate.Ticks)
                            continue;
                    }
                    Assert.Fail($"Unable to trim {dateTime.Ticks} to {p} period.");
                }
            }
        }
    }
}
