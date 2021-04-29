using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class TxNotificationMessageHandler : BaseAlphaMessageHandler
    {
        public TxNotificationMessageHandler(AlphaContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.TxNotification;

        public override bool IsAuditorOnly { get; } = true;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready, ConnectionState.Validated };

        public override Task HandleMessage(AlphaWebSocketConnection connection, IncomingMessage message)
        {
            Context.AuditLedgerManager.Add(message);
            return Task.CompletedTask;
        }
    }
}
