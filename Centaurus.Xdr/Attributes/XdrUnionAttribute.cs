using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Xdr
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class XdrUnionAttribute : Attribute
    {
        public XdrUnionAttribute(int discriminator, Type armType)
        {
            Discriminator = discriminator;
            ArmType = armType;
        }

        public int Discriminator { get; set; }

        public Type ArmType { get; set; }
    }
}
