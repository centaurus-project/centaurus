using MongoDB.Bson;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Mongo
{
    public static class PriceHistoryExtesnions
    {

        public static BsonObjectId EncodeId(int market, int period, int timestamp)
        {
            var rawId = new byte[12];
            BinaryPrimitives.WriteInt32BigEndian(rawId.AsSpan(0, 4), market);
            BinaryPrimitives.WriteInt32BigEndian(rawId.AsSpan(4, 4), period);
            BinaryPrimitives.WriteInt32BigEndian(rawId.AsSpan(8, 4), timestamp);
            return new BsonObjectId(new ObjectId(rawId));
        }

        public static (int market, int period, int timestamp) DecodeId(BsonObjectId id)
        {
            var bytes = id.Value.ToByteArray();
            return (
                market: BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(0, 4)),
                period: BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(4, 4)),
                timestamp: BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(8, 4)));
        }
    }
}
