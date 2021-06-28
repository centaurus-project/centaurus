using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message used as a quantum response.
    /// </summary>
    public class ResultMessage : Message
    {
        public override MessageTypes MessageType => MessageTypes.ResultMessage;

        [XdrField(0)]
        public MessageEnvelope OriginalMessage { get; set; }

        [XdrField(1)]
        public ResultStatusCodes Status { get; set; }

        [XdrField(3, Optional = true)]
        public string ErrorMessage { get; set; }

        public override long MessageId => OriginalMessage.Message.MessageId;
    }
}
