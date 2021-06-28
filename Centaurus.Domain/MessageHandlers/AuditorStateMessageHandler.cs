using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AuditorStateMessageHandler : MessageHandlerBase
    {
        public AuditorStateMessageHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorState;

        public override bool IsAuditorOnly { get; } = true;

        public override ConnectionState[] ValidConnectionStates => new ConnectionState[] { ConnectionState.Validated, ConnectionState.Ready };

        public override async Task HandleMessage(BaseWebSocketConnection connection, IncomingMessage message)
        {
            await Context.Catchup.AddAuditorState(connection.PubKey, (AuditorState)message.Envelope.Message);
        }
    }
}