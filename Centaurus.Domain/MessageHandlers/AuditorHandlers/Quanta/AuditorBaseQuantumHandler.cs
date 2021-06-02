using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public abstract class AuditorBaseQuantumHandler : BaseAuditorMessageHandler
    {
        public AuditorBaseQuantumHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override Task HandleMessage(OutgoingWebSocketConnection connection, IncomingMessage message)
        {
            _ = Context.QuantumHandler.HandleAsync(message.Envelope);
            return Task.CompletedTask;
        }
    }
}
