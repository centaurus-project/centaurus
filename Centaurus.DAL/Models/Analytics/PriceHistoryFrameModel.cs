using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models.Analytics
{
    public class PriceHistoryFrameModel
    {
        public const int OpenValueIndex = 0;
        public const int HighValueIndex = 1;
        public const int LowValueIndex = 2;
        public const int CloseValueIndex = 3;

        public BsonObjectId Id { get; set; }

        public double[] OHLC { get; set; } = new double[4];

        [BsonIgnore]
        public double Open
        {
            get => OHLC[OpenValueIndex];
            set => OHLC[OpenValueIndex] = value;
        }

        [BsonIgnore]
        public double Close
        {
            get => OHLC[CloseValueIndex];
            set => OHLC[CloseValueIndex] = value;
        }

        [BsonIgnore]
        public double High
        {
            get => OHLC[HighValueIndex];
            set => OHLC[HighValueIndex] = value;
        }

        [BsonIgnore]
        public double Low
        {
            get => OHLC[LowValueIndex];
            set => OHLC[LowValueIndex] = value;
        }

        public double BaseVolume { get; set; }

        public double CounterVolume { get; set; }
    }
}