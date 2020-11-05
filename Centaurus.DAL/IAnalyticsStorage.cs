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
        Task<List<OHLCFrameModel>> GetFrames(int fromUnixTimeStamp, int toUnixTimeStamp, int asset, OHLCFramePeriod period);

        Task SaveAnalytics(List<OHLCFrameModel> frames);

        Task<int> GetFirstFrameDate(OHLCFramePeriod period);
    }
}
