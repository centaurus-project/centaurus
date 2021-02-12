using MongoDB.Bson;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.DAL
{
    public static class EffectModelIdConverter
    {
        public static BsonObjectId EncodeId(long apex, int account)
        {
            var rawId = new byte[12];
            BinaryPrimitives.WriteInt64BigEndian(rawId.AsSpan(0, 8), apex);
            BinaryPrimitives.WriteInt32BigEndian(rawId.AsSpan(8, 4), account);
            return new BsonObjectId(new ObjectId(rawId));
        }

        public static (long apex, int account) DecodeId(byte[] effectId)
        {
            if (effectId == null || effectId.Length != sizeof(long) + sizeof(int))
                throw new ArgumentException("Invalid effect id format.", nameof(effectId));
            return (
                apex: BinaryPrimitives.ReadInt64BigEndian(effectId.AsSpan(0, 8)),
                account: BinaryPrimitives.ReadInt32BigEndian(effectId.AsSpan(8, 4))
            );
        }

        public static (long apex, int account) DecodeId(BsonObjectId effectId)
        {
            if (effectId == null)
                throw new ArgumentNullException(nameof(effectId));
            return DecodeId(effectId.Value.ToByteArray());
        }
    }
}
