using System;
using System.Buffers.Binary;

namespace Centaurus.PersistentStorage
{
    public static class ApexConverter
    {
        public static byte[] EncodeApex(ulong apex)
        {
            var res = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(res, apex);
            return res;
        }

        public static ulong DecodeApex(byte[] value)
        {
            return BinaryPrimitives.ReadUInt64BigEndian(value);
        }
    }
}
