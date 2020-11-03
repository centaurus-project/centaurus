using Centaurus.Analytics;
using Centaurus.DAL.Models.Analytics;
using System;

namespace Centaurus.Exchange.Analytics
{
    public static class OHLCFrameExtensions
    {
        public static OHLCFrame FromModel(this OHLCFrameModel frameModel)
        {
            if (frameModel is null)
                throw new ArgumentNullException(nameof(frameModel));
            return new OHLCFrame(DateTimeOffset.FromUnixTimeSeconds(frameModel.TimeStamp).UtcDateTime, (OHLCFramePeriod)frameModel.Period, frameModel.Market)
            {
                Open = frameModel.Open,
                Close = frameModel.Close,
                Hi = frameModel.Hi,
                Low = frameModel.Low,
                Volume = frameModel.Volume
            };
        }

        public static OHLCFrameModel ToFrameModel(this OHLCFrame frame)
        {
            if (frame is null)
                throw new ArgumentNullException(nameof(frame));
            return new OHLCFrameModel
            {
                TimeStamp = (int)((DateTimeOffset)frame.StartTime).ToUnixTimeSeconds(),
                Market = frame.Market,
                Open = frame.Open,
                Close = frame.Close,
                Low = frame.Low,
                Hi = frame.Hi,
                Period = (int)frame.Period,
                Volume = frame.Volume
            };
        }
    }
}