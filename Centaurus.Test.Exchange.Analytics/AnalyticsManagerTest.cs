using Centaurus.Analytics;
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

    public class AnalyticsManagerTest: BaseAnalyticsTest
    {

        [Test]
        public async Task RestoreTest()
        {
            GenerateTrades(10_000);
            await analyticsManager.SaveUpdates(storage);

            var restoredAnalyticsManager = new AnalyticsManager(storage, markets, historyLength);
            await restoredAnalyticsManager.Restore(new DateTime(now, DateTimeKind.Utc));

            foreach (var market in markets)
            {
                foreach (var period in Enum.GetValues(typeof(OHLCFramePeriod)))
                {
                    var frames = await analyticsManager.OHLCManager.GetPeriod(0, market, (OHLCFramePeriod)period);
                    var restoredFrames = await restoredAnalyticsManager.OHLCManager.GetPeriod(0, market, (OHLCFramePeriod)period);
                    Assert.AreEqual(frames.frames.Count, restoredFrames.frames.Count, "Current frames unit and restored frames unit have differnt size.");
                    for (var i = 0; i < frames.frames.Count; i++)
                    {
                        var frame = frames.frames[i];
                        var restoredFrame = restoredFrames.frames[i];
                        Assert.AreEqual(frame, restoredFrame, "Restored frame doesn't equal to current frame.");
                    }
                }
            }
        }
    }
}
