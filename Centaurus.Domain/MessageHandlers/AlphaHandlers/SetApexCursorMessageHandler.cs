using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class SetApexCursorMessageHandler : BaseAlphaMessageHandler
    {
        public SetApexCursorMessageHandler(AlphaContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.SetApexCursor;

        public override bool IsAuditorOnly { get; } = true;

        public override ConnectionState[] ValidConnectionStates { get; } =
            new ConnectionState[] {
                ConnectionState.Validated,
                ConnectionState.Ready
            };

        public override Task HandleMessage(AlphaWebSocketConnection connection, IncomingMessage message)
        {
            connection.ResetApexCursor(message.Envelope.Message as SetApexCursor);
            return Task.CompletedTask;
        }
    }
}
