using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class RequestQuantumHandler : AuditorBaseQuantumHandler
    {
        public RequestQuantumHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.RequestQuantum;

        public override async Task Validate(OutgoingWebSocketConnection connection, IncomingMessage message)
        {
            await base.Validate(connection, message);

            var clientRequest = (message.Envelope.Message as RequestQuantum)?.RequestEnvelope;

            if (clientRequest == null)
                throw new UnexpectedMessageException("RequestQuantum is expected");

            if (!clientRequest.AreSignaturesValid())
                throw new UnauthorizedException();
        }
    }
}
