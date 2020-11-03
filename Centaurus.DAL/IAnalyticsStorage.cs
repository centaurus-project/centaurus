using Centaurus.Analytics;
using Centaurus.DAL.Models.Analytics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.DAL
{
    public interface IAnalyticsStorage
    {
        Task<List<OHLCFrameModel>> GetFrames(int unixTimeStamp, int asset, OHLCFramePeriod period, int limit = 1000);

        Task SaveAnalytics(List<OHLCFrameModel> frames);
    }
}
