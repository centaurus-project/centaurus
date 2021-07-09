using System;
using System.Buffers.Binary;
using MessagePack;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class PriceHistoryFramePersistentModel : IPersistentModel, IPrefixedPersistentModel
    {
        [IgnoreMember]
        public int Period { get; set; }

        [IgnoreMember]
        public int Timestamp { get; set; }

        [IgnoreMember]
        public string Market { get; set; }

        [Key(0)]
        public double[] OHLC { get; set; } = new double[4];

        [Key(1)]
        public double BaseVolume { get; set; }

        [Key(2)]
        public double CounterVolume { get; set; }

        [IgnoreMember]
        public double Open
        {
            get => OHLC[0];
            set => OHLC[0] = value;
        }

        [IgnoreMember]
        public double High
        {
            get => OHLC[1];
            set => OHLC[1] = value;
        }

        [IgnoreMember]
        public double Low
        {
            get => OHLC[2];
            set => OHLC[2] = value;
        }

        [IgnoreMember]
        public double Close
        {
            get => OHLC[3];
            set => OHLC[3] = value;
        }

        public byte[] Key
        {
            get
            {
                var encodedMarket = MessagePackSerializer.Serialize(Market);
                var key = new byte[8 + encodedMarket.Length];
                BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(0, 4), Period);
                BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(4, 4), Timestamp);
                encodedMarket.CopyTo(key.AsSpan(key.Length - 8));
                return key;
            }
            set
            {
                Period = BinaryPrimitives.ReadInt32BigEndian(value.AsSpan(0, 4));
                Timestamp = BinaryPrimitives.ReadInt32BigEndian(value.AsSpan(4, 4));
                Market = MessagePackSerializer.Deserialize<string>(value.AsMemory(8, value.Length - 8));
            }
        }

        public string ColumnFamily => "pricehistory";
        public uint PrefixLength => 8;
    }
}
