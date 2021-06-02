using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class AuditorClientsRequestMessageHandler : BaseAuditorMessageHandler
    {
        protected AuditorClientsRequestMessageHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override async Task Validate(OutgoingWebSocketConnection connection, IncomingMessage message)
        {
            var request = message.Envelope.Message as RequestMessage;
            if (request == null)
                throw new BadRequestException("Message of RequestMessage was expected");
            await base.Validate(connection, message);
            if (!message.Envelope.IsSignedBy(request.AccountWrapper.Account.Pubkey))
                throw new UnauthorizedException();
        }
    }
}
