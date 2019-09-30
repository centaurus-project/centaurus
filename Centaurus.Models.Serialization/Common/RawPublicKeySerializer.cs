using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class RawPublicKeySerializer : BinaryDataSerializer<RawPubKey>, IXdrSerializer<RawPubKey>
    {
    }
}
