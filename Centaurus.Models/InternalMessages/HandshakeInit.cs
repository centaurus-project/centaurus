using System.Text;

namespace Centaurus.Models
{
    public class HandshakeInit : Message
    {
        public override MessageTypes MessageType => MessageTypes.HandshakeInit;

        [XdrField(0)]
        public HandshakeData HandshakeData { get; set; }

        [XdrField(1, Optional = true)]
        public HandshakePayload Payload { get; set; }
    }

    [XdrContract]
    [XdrUnion(0, typeof(AuditorHandshakePayload))]
    public abstract class HandshakePayload
    {

    }

    public class AuditorHandshakePayload: HandshakePayload
    {
        [XdrField(0)]
        public long Apex { get; set; }
    }
}
