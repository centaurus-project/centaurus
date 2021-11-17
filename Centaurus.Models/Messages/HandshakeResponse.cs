using Centaurus.Xdr;
using System.Collections.Generic;

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
    }

    public class AuditorHandshakeResponse : HandshakeResponseBase
    {

        [XdrField(0)]
        public State State { get; set; }

        [XdrField(1)]
        public List<SyncCursor> Cursors { get; set; }
    }
}
