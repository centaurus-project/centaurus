using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;

namespace Centaurus.Exchange.Analytics
{
    public static class PriceHistoryFrameExtensions
    {
        public static PriceHistoryFrame FromFramePersistentModel(this PriceHistoryFramePersistentModel frameModel)
        {
            if (frameModel is null)
                throw new ArgumentNullException(nameof(frameModel));

            return new PriceHistoryFrame(DateTimeOffset.FromUnixTimeSeconds(frameModel.Timestamp).UtcDateTime, (PriceHistoryPeriod)frameModel.Period, frameModel.Market, frameModel.Open)
            {
                Open = frameModel.Open,
                Close = frameModel.Close,
                High = frameModel.High,
                Low = frameModel.Low,
                BaseVolume = frameModel.BaseVolume,
                CounterVolume = frameModel.CounterVolume
            };
        }

        public static PriceHistoryFramePersistentModel ToFramePersistentModel(this PriceHistoryFrame frame)
        {
            if (frame is null)
                throw new ArgumentNullException(nameof(frame));

            return new PriceHistoryFramePersistentModel
            {
                Open = frame.Open,
                Close = frame.Close,
                Low = frame.Low,
                High = frame.High,
                BaseVolume = frame.BaseVolume,
                CounterVolume = frame.CounterVolume,
                Market = frame.Market,
                Period = (int)frame.Period,
                Timestamp = (int)((DateTimeOffset)frame.StartTime).ToUnixTimeSeconds()
            };
        }
    }
}