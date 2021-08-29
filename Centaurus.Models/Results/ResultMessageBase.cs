using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message used as a message response.
    /// </summary>
    public abstract class ResultMessageBase : Message
    {
        [XdrField(0)]
        public MessageEnvelopeBase OriginalMessage { get; set; }

        [XdrField(1)]
        public ResultStatusCode Status { get; set; }

        [XdrField(3, Optional = true)]
        public string ErrorMessage { get; set; }

        public override long MessageId => OriginalMessage.Message.MessageId;
    }
}