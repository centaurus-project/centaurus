using System;
using System.Buffers.Binary;
using System.Text;
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

        [IgnoreMember]
        public byte[] Key
        {
            get
            {
                var encodedMarket = Encoding.UTF8.GetBytes(Market);
                var key = new byte[12];
                encodedMarket.CopyTo(key.AsSpan(0, 4));
                BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(4, 4), Period);
                BinaryPrimitives.WriteInt32BigEndian(key.AsSpan(8, 4), Timestamp);
                return key;
            }
            set
            {
                Market = Encoding.UTF8.GetString(value.AsSpan(0, 4).TrimEnd((byte)0));
                Period = BinaryPrimitives.ReadInt32BigEndian(value.AsSpan(4, 4));
                Timestamp = BinaryPrimitives.ReadInt32BigEndian(value.AsSpan(8, 4));
            }
        }

        [IgnoreMember]
        public string ColumnFamily => "pricehistory";

        [IgnoreMember]
        public uint PrefixLength => 8;
    }
}
