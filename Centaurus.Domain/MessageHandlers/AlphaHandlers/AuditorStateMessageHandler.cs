using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AuditorStateMessageHandler : BaseAlphaMessageHandler
    {
        public AuditorStateMessageHandler(AlphaContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorState;

        public override bool IsAuditorOnly { get; } = true;

        public override ConnectionState[] ValidConnectionStates { get; } = 
            new ConnectionState[] {
                ConnectionState.Validated,
                ConnectionState.Ready
            };

        public override async Task HandleMessage(AlphaWebSocketConnection connection, IncomingMessage message)
        {
            await Context.Catchup.AddAuditorState(connection.ClientPubKey, (AuditorState)message.Envelope.Message);
        }
    }
}
