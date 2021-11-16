using Centaurus.Models;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class StateUpdateMessageHandler : MessageHandlerBase
    {
        public StateUpdateMessageHandler(ExecutionContext executionContext)
            : base(executionContext)
        {

        }

        public override string SupportedMessageType => typeof(StateUpdateMessage).Name;

        public override bool IsAuditorOnly => true;

        public override Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            Context.StateManager.SetAuditorState(connection.PubKey, ((StateUpdateMessage)message.Envelope.Message).State);
            return Task.CompletedTask;
        }
    }
}
