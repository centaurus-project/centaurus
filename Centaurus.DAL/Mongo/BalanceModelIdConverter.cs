using MongoDB.Bson;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Mongo
{
    public class BalanceModelIdConverter
    {
        public static BsonObjectId EncodeId(int account, int asset)
        {
            var rawId = new byte[12];
            BinaryPrimitives.WriteInt32BigEndian(rawId.AsSpan(0, 4), account);
            BinaryPrimitives.WriteInt32BigEndian(rawId.AsSpan(4, 4), asset);
            return new BsonObjectId(new ObjectId(rawId));
        }

        public static (int account, int asset) DecodeId(byte[] assetId)
        {
            if (assetId == null || assetId.Length != sizeof(long) + sizeof(int))
                throw new ArgumentException("Invalid effect id format.", nameof(assetId));
            return (
                account: BinaryPrimitives.ReadInt32BigEndian(assetId.AsSpan(0, 4)),
                asset: BinaryPrimitives.ReadInt32BigEndian(assetId.AsSpan(4, 4))
            );
        }

        public static (int account, int asset) DecodeId(BsonObjectId assetId)
        {
            if (assetId == null)
                throw new ArgumentNullException(nameof(assetId));
            return DecodeId(assetId.Value.ToByteArray());
        }
    }
}
