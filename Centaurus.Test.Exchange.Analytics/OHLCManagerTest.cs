using Centaurus.Analytics;
using Centaurus.Exchange.Analytics;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test.Exchange.Analytics
{
    public class OHLCManagerTest : BaseAnalyticsTest
    {
        [Test]
        public async Task GetPeriodTest()
        {
            GenerateTrades(50_000);

            foreach (var market in markets)
            {
                foreach (var period in Enum.GetValues(typeof(OHLCFramePeriod)))
                {
                    var prevPeriodFrame = default(OHLCFrame);
                    var cursor = 0;
                    do
                    {
                        var periodRespond = await analyticsManager.OHLCManager.GetPeriod(cursor, market, (OHLCFramePeriod)period);
                        foreach (var frame in periodRespond.frames)
                        {
                            Assert.GreaterOrEqual(frame.Hi, frame.Low, "Frame Hi price is greater than Low price.");
                            Assert.Greater(frame.Volume, 0, "Volume must be greater than 0.");
                            if (prevPeriodFrame != null)
                                Assert.Greater(prevPeriodFrame.StartTime, frame.StartTime, "Current frame start time cannot greater or equal to the previous one.");
                            prevPeriodFrame = frame;
                        }
                        cursor = periodRespond.nextCursor;
                    }
                    while (cursor != 0);
                }
            }
        }
    }
}
