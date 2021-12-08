using Centaurus.Models;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal class StateMessageHandler : MessageHandlerBase
    {
        public StateMessageHandler(ExecutionContext executionContext)
            : base(executionContext)
        {

        }

        public override string SupportedMessageType => typeof(StateMessage).Name;

        public override bool IsAuditorOnly => true;

        public override ConnectionState[] ValidConnectionStates => new[] { ConnectionState.Ready };

        public override Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            var nodeConnection = (INodeConnection)connection;
            nodeConnection.Node.Update((StateMessage)message.Envelope.Message);
            return Task.CompletedTask;
        }
    }
}
