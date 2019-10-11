using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class AlphaResultMessageHandler : BaseAlphaMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.ResultMessage;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override bool IsAuditorOnly { get; } = true;

        public override async Task HandleMessage(AlphaWebSocketConnection connection, MessageEnvelope envelope)
        {
            await Global.AuditResultManager.Add(envelope);
        }
    }
}