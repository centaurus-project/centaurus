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
            return new OHLCFrame(DateTimeOffset.FromUnixTimeSeconds(frameModel.TimeStamp).UtcDateTime, (OHLCFramePeriod)frameModel.Period, frameModel.Market, frameModel.Open)
            {
                Open = frameModel.Open,
                Close = frameModel.Close,
                High = frameModel.High,
                Low = frameModel.Low,
                BaseAssetVolume = frameModel.BaseAssetVolume,
                MarketAssetVolume = frameModel.MarketAssetVolume
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
                High = frame.High,
                Period = (int)frame.Period,
                BaseAssetVolume = frame.BaseAssetVolume,
                MarketAssetVolume = frame.MarketAssetVolume
            };
        }
    }
}