using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Centaurus.Models
{
    public class HandshakeDataSerializer : BinaryDataSerializer<HandshakeData>, IXdrSerializer<HandshakeData>
    {
    }
}
