using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class HandshakeRequest : Message
    {
        [XdrField(0)]
        public HandshakeData HandshakeData { get; set; }

        public override long MessageId => HandshakeData?.Data.GetInt64Fingerprint() ?? 0;
    }
}
