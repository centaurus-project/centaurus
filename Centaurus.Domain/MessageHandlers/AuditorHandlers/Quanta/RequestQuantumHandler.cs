using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class RequestQuantumHandler : AuditorBaseQuantumHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.RequestQuantum;

        public override async Task Validate(AuditorWebSocketConnection connection, MessageEnvelope envelope)
        {
            await base.Validate(connection, envelope);

            var clientRequest = (envelope.Message as RequestQuantum)?.RequestEnvelope;

            if (clientRequest == null)
                throw new UnexpectedMessageException("RequestQuantum is expected");

            if (!clientRequest.AreSignaturesValid())
                throw new UnauthorizedException();
        }
    }
}
