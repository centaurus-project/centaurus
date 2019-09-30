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

        public MessageEnvelope OriginalMessage { get; set; }

        public ResultStatusCodes Status { get; set; }

        public List<Effect> Effects { get; set; }

        public override ulong MessageId => OriginalMessage.Message.MessageId;
    }
}
