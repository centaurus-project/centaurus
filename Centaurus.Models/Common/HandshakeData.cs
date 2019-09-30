using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Centaurus.Models
{
    public class HandshakeData: BinaryData
    {
        public override int ByteLength => 32;

        public static implicit operator HandshakeData(byte[] data)
        {
            return new HandshakeData() { Data = data };
        }
    }
}
