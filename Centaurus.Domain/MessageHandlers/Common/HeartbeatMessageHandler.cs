using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class HeartbeatMessageHandler : BaseMessageHandler<BaseWebSocketConnection>, IAlphaMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.Heartbeat;

        public override ConnectionState[] ValidConnectionStates { get; } = null;

        public override Task HandleMessage(BaseWebSocketConnection connection, MessageEnvelope messageEnvelope)
        {
            return Task.CompletedTask;
        }
    }
}
