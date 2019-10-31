using stellar_dotnet_sdk.xdr;
using System.Collections.Generic;

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

        [XdrField(2)]
        public List<Effect> Effects { get; set; }

        public override ulong MessageId => OriginalMessage.Message.MessageId;
    }
}
