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

        public override Task HandleMessage(AlphaWebSocketConnection connection, MessageEnvelope envelope)
        {
            var quantum = Global.AuditLedgerManager.Add(envelope);
            if (quantum != null)
            {
                //create envelope and sign it
                var ledgerCommitEnvelope = quantum.CreateEnvelope();
                Global.QuantumHandler.Handle(ledgerCommitEnvelope);
            }
            return Task.CompletedTask;
        }
    }
}
