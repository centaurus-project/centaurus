using Centaurus.Models;
using System;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AlphaEffectsRequestMessageHandler : BaseAlphaMessageHandler
    {
        public override bool IsAuditorOnly => false;

        public override MessageTypes SupportedMessageType => MessageTypes.EffectsRequest;

        public override ConnectionState[] ValidConnectionStates => new ConnectionState[] { ConnectionState.Ready };

        public override Task HandleMessage(AlphaWebSocketConnection connection, MessageEnvelope messageEnvelope)
        {
            Notifier.NotifyAuditors(messageEnvelope);
            return Task.CompletedTask;
        }
    }
}
