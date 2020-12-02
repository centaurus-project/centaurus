using Centaurus.Models;
using Centaurus.DAL.Models.Analytics;
using System;
using Centaurus.DAL.Mongo;

namespace Centaurus.Exchange.Analytics
{
    public static class PriceHistoryFrameExtensions
    {
        public static PriceHistoryFrame FromModel(this PriceHistoryFrameModel frameModel)
        {
            if (frameModel is null)
                throw new ArgumentNullException(nameof(frameModel));

            var decodedId = PriceHistoryExtesnions.DecodeId(frameModel.Id);
            return new PriceHistoryFrame(DateTimeOffset.FromUnixTimeSeconds(decodedId.timestamp).UtcDateTime, (PriceHistoryPeriod)decodedId.period, decodedId.market, frameModel.Open)
            {
                Open = frameModel.Open,
                Close = frameModel.Close,
                High = frameModel.High,
                Low = frameModel.Low,
                BaseVolume = frameModel.BaseVolume,
                CounterVolume = frameModel.CounterVolume
            };
        }

        public static PriceHistoryFrameModel ToFrameModel(this PriceHistoryFrame frame)
        {
            if (frame is null)
                throw new ArgumentNullException(nameof(frame));

            var id = PriceHistoryExtesnions.EncodeId(frame.Market, (int)frame.Period, (int)((DateTimeOffset)frame.StartTime).ToUnixTimeSeconds());
            return new PriceHistoryFrameModel
            {
                Id = id,
                Open = frame.Open,
                Close = frame.Close,
                Low = frame.Low,
                High = frame.High,
                BaseVolume = frame.BaseVolume,
                CounterVolume = frame.CounterVolume
            };
        }
    }
}