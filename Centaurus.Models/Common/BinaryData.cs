using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public abstract class BinaryData : IEquatable<BinaryData>
    {
        public abstract int ByteLength { get; }

        private byte[] _Data;

        [XdrField(0)]
        public byte[] Data
        {
            get
            {
                return _Data;
            }
            set
            {
                ByteArrayPrimitives.CheckBufferLength(value, ByteLength);
                _Data = value;
            }
        }

        public override int GetHashCode()
        {
            return ByteArrayPrimitives.GetHashCode(Data);
        }

        public override string ToString()
        {
            return Data.ToHex();
        }

        public byte[] ToArray()
        {
            return Data;
        }

        public bool Equals(BinaryData other)
        {
            if (other == null) return false;
            return ByteArrayPrimitives.Equals(Data, other.Data);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BinaryData)) return false;
            return Equals((BinaryData)obj);
        }
    }
}
