using System;
using System.Buffers.Binary;

namespace Centaurus.PersistentStorage
{
    public static class UlongConverter
    {
        public static byte[] Encode(ulong apex)
        {
            var res = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(res, apex);
            return res;
        }

        public static ulong Decode(byte[] value)
        {
            return BinaryPrimitives.ReadUInt64BigEndian(value);
        }
    }
}
