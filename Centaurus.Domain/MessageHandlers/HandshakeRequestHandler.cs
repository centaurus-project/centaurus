using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class HandshakeRequestHandler : MessageHandlerBase<OutgoingConnection>
    {
        public HandshakeRequestHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(HandshakeRequest).Name;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Validated };

        public override async Task HandleMessage(OutgoingConnection connection, IncomingMessage message)
        {
            var handshakeRequest = (HandshakeRequest)message.Envelope.Message;

            var lastKnownApex = Math.Max(Context.QuantumHandler.LastAddedQuantumApex, Context.QuantumStorage.CurrentApex);
            await connection.SendMessage(new AuditorHandshakeResponse
            {
                HandshakeData = handshakeRequest.HandshakeData,
                LastKnownApex = lastKnownApex
            });

            //after sending auditor handshake the connection become ready
            connection.ConnectionState = ConnectionState.Ready;
        }
    }
}
