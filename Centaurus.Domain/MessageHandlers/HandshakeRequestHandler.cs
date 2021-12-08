using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    internal class HandshakeRequestHandler : MessageHandlerBase<OutgoingConnection>
    {
        public HandshakeRequestHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(HandshakeRequest).Name;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override async Task HandleMessage(OutgoingConnection connection, IncomingMessage message)
        {
            var handshakeRequest = (HandshakeRequest)message.Envelope.Message;

            var quantaCursor = new SyncCursor { Type = XdrSyncCursorType.Quanta, Cursor = Context.QuantumHandler.LastAddedQuantumApex };

            //if Prime than the node will receive results from auditors
            var signaturesCursor = new SyncCursor
            {
                Type = XdrSyncCursorType.Signatures,
                Cursor = Context.PendingUpdatesManager.LastPersistedApex,
                DisableSync = Context.RoleManager.ParticipationLevel == CentaurusNodeParticipationLevel.Prime
            };
            await connection.SendMessage(new HandshakeResponse
            {
                HandshakeData = handshakeRequest.HandshakeData
            });

            //after sending auditor handshake the connection becomes ready
            connection.ConnectionState = ConnectionState.Ready;
        }
    }
}
