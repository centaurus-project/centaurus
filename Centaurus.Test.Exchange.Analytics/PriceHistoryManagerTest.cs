using Centaurus.Models;
using Centaurus.Exchange.Analytics;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace Centaurus.Test.Exchange.Analytics
{
    public class PriceHistoryManagerTest : BaseAnalyticsTest
    {
        [Test]
        public void GetPeriodTest()
        {
            GenerateTrades(50_000);

            foreach (var market in markets)
            {
                foreach (var period in Enum.GetValues(typeof(PriceHistoryPeriod)).Cast<PriceHistoryPeriod>())
                {
                    var prevFrame = default(PriceHistoryFrame);
                    var cursor = 0;
                    do
                    {
                        var periodResponse = analyticsManager.PriceHistoryManager.GetPriceHistory(cursor, market, period);

                        for (var i = 0; i < periodResponse.frames.Count; i++)
                        {
                            var frame = periodResponse.frames[i];

                            Assert.GreaterOrEqual(frame.High, frame.Low, "Frame High price is greater than Low price.");
                            if (frame.HadTrades)
                            {
                                Assert.Greater(frame.BaseVolume, 0, "Base asset volume must be greater than 0.");
                                Assert.Greater(frame.CounterVolume, 0, "Market asset volume must be greater than 0.");

                                Assert.Greater(frame.High, 0, "High price must be greater than 0.");
                                Assert.Greater(frame.Low, 0, "Low price must be greater than 0.");
                            }
                            else
                            {
                                Assert.AreEqual(frame.BaseVolume, 0, "Base asset volume must be equal to 0, if frame has no trades.");
                                Assert.AreEqual(frame.CounterVolume, 0, "Market asset volume must be equal to 0, if frame has no trades.");

                                Assert.AreEqual(frame.High, 0, "High price must be equal to 0, if frame has no trades.");
                                Assert.AreEqual(frame.Low, 0, "Low price must be equal to 0, if frame has no trades.");
                            }

                            if (!prevFrame?.HadTrades ?? false)
                            {
                                Assert.AreEqual(frame.Close, prevFrame.Open, "Close price must be equal to previous frame Open price, if frame has no trades.");
                                Assert.AreEqual(frame.Close, prevFrame.Close, "Close price must be equal to previous frame Close price, if frame has no trades.");
                            }
                            if (prevFrame != null)
                            {
                                Assert.AreEqual(frame.StartTime.GetNextFrameDate(period), prevFrame.StartTime, $"Difference between current frame start time and previous frame start time must be equal to single {period} period.");
                            }

                            prevFrame = frame;
                        }
                        cursor = periodResponse.nextCursor;
                    }
                    while (cursor != 0);
                }
            }
        }
    }
}
