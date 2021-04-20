using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public abstract class BaseAuditorMessageHandler: BaseMessageHandler<AuditorWebSocketConnection, AuditorContext>, IAuditorMessageHandler
    {
        public BaseAuditorMessageHandler(AuditorContext context) 
            : base(context)
        {
        }

        public override async Task Validate(AuditorWebSocketConnection connection, IncomingMessage message)
        {
            //validate that alpha has signed the message
            if (!message.Envelope.IsSignedBy(Context.Settings.AlphaKeyPair))
                throw new UnauthorizedException();
            await base.Validate(connection, message);
        }
    }
}
