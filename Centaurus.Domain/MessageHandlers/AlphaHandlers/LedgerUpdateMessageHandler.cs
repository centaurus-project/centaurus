using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class LedgerUpdateMessageHandler : BaseAlphaMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.LedgerUpdateNotification;

        public override bool IsAuditorOnly { get; } = true;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override async Task HandleMessage(AlphaWebSocketConnection connection, MessageEnvelope envelope)
        {
            await Global.AuditLedgerManager.Add(envelope);
        }
    }
}
