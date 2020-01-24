using Centaurus.Xdr;

namespace Centaurus.Models
{
    public class HandshakeInit : Message
    {
        public override MessageTypes MessageType => MessageTypes.HandshakeInit;

        [XdrField(0)]
        public HandshakeData HandshakeData { get; set; }
    }
}
