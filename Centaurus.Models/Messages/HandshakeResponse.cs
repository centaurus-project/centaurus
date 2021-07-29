using Centaurus.Xdr;

namespace Centaurus.Models
{
    public abstract class HandshakeResponseBase : Message
    {
        [XdrField(0)]
        public HandshakeData HandshakeData { get; set; }

        public override long MessageId => HandshakeData?.Data.GetInt64Fingerprint() ?? 0;
    }

    public class HandshakeResponse : HandshakeResponseBase
    {
        public override MessageTypes MessageType => MessageTypes.HandshakeResponse;
    }

    public class AuditorHandshakeResponse : HandshakeResponseBase
    {
        public override MessageTypes MessageType => MessageTypes.AuditorHandshakeResponse;

        [XdrField(0)]
        public ulong LastKnownApex { get; set; }
    }
}
