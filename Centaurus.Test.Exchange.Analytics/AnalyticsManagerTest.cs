using Centaurus.Models;
using Centaurus.DAL;
using Centaurus.Exchange.Analytics;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test.Exchange.Analytics
{
    public class AnalyticsManagerTest : BaseAnalyticsTest
    {

        [Test]
        public async Task RestoreTest()
        {
            GenerateTrades(10_000);
            await analyticsManager.SaveUpdates(storage);

            var restoredAnalyticsManager = new AnalyticsManager(storage, new List<double> { 1 }, markets, new List<OrderInfo>(), historyLength);
            await restoredAnalyticsManager.Restore(now);

            foreach (var market in markets)
            {
                foreach (var period in Enum.GetValues(typeof(PriceHistoryPeriod)).Cast<PriceHistoryPeriod>())
                {
                    var frames = await analyticsManager.PriceHistoryManager.GetPriceHistory(0, market, period);
                    var restoredFrames = await restoredAnalyticsManager.PriceHistoryManager.GetPriceHistory(0, market, period);
                    Assert.AreEqual(frames.frames.Count, restoredFrames.frames.Count, "Current frames unit and restored frames unit have different size.");
                    for (var i = 0; i < frames.frames.Count; i++)
                    {
                        var frame = frames.frames[i];
                        var restoredFrame = restoredFrames.frames[i];

                        Assert.IsTrue(frame.StartTime == restoredFrame.StartTime &&
                           frame.Period == restoredFrame.Period &&
                           frame.Market == restoredFrame.Market &&
                           frame.High == restoredFrame.High &&
                           frame.Low == restoredFrame.Low &&
                           frame.Open == restoredFrame.Open &&
                           frame.Close == restoredFrame.Close &&
                           frame.BaseVolume == restoredFrame.BaseVolume && 
                           frame.CounterVolume == restoredFrame.CounterVolume,
                           "Restored frame doesn't equal to current frame.");
                    }
                }
            }
        }
    }
}
