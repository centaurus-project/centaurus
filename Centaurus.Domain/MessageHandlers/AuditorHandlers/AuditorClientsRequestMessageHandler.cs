using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class AuditorClientsRequestMessageHandler : BaseAuditorMessageHandler
    {
        public override async Task Validate(AuditorWebSocketConnection connection, MessageEnvelope envelope)
        {
            var request = envelope.Message as RequestMessage;
            if (request == null)
                throw new BadRequestException("Message of RequestMessage was expected");
            await base.Validate(connection, envelope);
            if (!envelope.IsSignedBy(request.AccountWrapper.Account.Pubkey))
                throw new UnauthorizedException();
        }
    }
}
