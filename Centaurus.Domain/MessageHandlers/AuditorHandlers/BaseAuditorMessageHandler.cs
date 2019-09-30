using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class BaseAuditorMessageHandler: BaseMessageHandler<AuditorWebSocketConnection>, IAuditorMessageHandler
    {
    }
}
