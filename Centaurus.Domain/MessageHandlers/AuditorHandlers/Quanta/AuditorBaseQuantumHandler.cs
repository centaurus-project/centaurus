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
        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override async Task Validate(AuditorWebSocketConnection connection, MessageEnvelope envelope)
        {
            //validate that alpha has signed the quantum request
            if (!envelope.IsSignedBy(((AuditorSettings)Global.Settings).AlphaKeyPair.PublicKey))
                throw new UnauthorizedException();
            await base.Validate(connection, envelope);
        }

        public override Task HandleMessage(AuditorWebSocketConnection connection, MessageEnvelope messageEnvelope)
        {
            Global.QuantumHandler.Handle(messageEnvelope);
            return Task.CompletedTask;
        }
    }
}
