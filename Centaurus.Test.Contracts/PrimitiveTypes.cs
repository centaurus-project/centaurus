using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Test.Contracts
{
    [XdrContract]
    public class PrimitiveTypes
    {
        [XdrField(0)]
        public bool Bool { get; set; }

        [XdrField(1)]
        public int Int32 { get; set; }

        [XdrField(2)]
        public uint UInt32 { get; set; }

        [XdrField(3)]
        public long Int64 { get; set; }

        [XdrField(4)]
        public ulong UInt64 { get; set; }

        [XdrField(5)]
        public double Double { get; set; }

        [XdrField(6)]
        public string String { get; set; }

        [XdrField(7)]
        public byte[] ByteArray { get; set; }

        [XdrField(8)]
        public EnumInt32Values EnumInt32 { get; set; }

        [XdrField(9, Optional = true)]
        public List<int> Int32List { get; set; }

        [XdrField(10)]
        public double[] DoubleArray { get; set; }

        //[XdrField(11)]
        public int? Int32Nullable { get; set; }

        public string NotSearialized { get; set; }
    }

    public enum EnumInt32Values
    {
        Zero = 0,
        One = 1,
        Two = 2
    }
}
