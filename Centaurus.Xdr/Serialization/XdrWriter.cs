using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Xdr
{
    public abstract class XdrWriter
    {
        protected const int DefaultBufferSize = 64 * 1024; //64KB

        protected static readonly Encoding StringEncoding = Encoding.UTF8;

        protected static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Create();

        public abstract int Position { get; }

        public abstract void WriteInt32(int value);

        public abstract void WriteUInt32(uint value);

        public abstract void WriteInt64(long value);

        public abstract void WriteUInt64(ulong value);

        public abstract void WriteFloat(float value);

        public abstract void WriteDouble(double value);

        public abstract void WriteBoolean(bool value);

        public abstract void WriteEnum(Enum value);

        public abstract void WriteString(string value);

        public abstract void WriteVariable(byte[] value, int? count = null);

        public abstract void WriteObject(object value, Type type);
    }
}