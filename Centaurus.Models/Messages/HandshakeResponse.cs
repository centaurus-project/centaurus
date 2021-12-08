using Centaurus.Xdr;
using System.Collections.Generic;

namespace Centaurus.Models
{
    public class HandshakeResponse : Message
    {
        [XdrField(0)]
        public HandshakeData HandshakeData { get; set; }

        public override long MessageId => HandshakeData?.Data.GetInt64Fingerprint() ?? 0;
    }
}
