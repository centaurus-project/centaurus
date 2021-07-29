using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class HandshakeRequestHandler : MessageHandlerBase
    {
        public HandshakeRequestHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.HandshakeRequest;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override async Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            var handshakeRequest = (HandshakeRequest)message.Envelope.Message;

            var lastKnownApex = Context.QuantumHandler.LastAddedQuantumApex > 0
                ? Context.QuantumHandler.LastAddedQuantumApex
                : Context.QuantumStorage.CurrentApex;

            if (connection is OutgoingConnection) //if connection is an outgoing than the other side is an auditor
                await connection.SendMessage(new AuditorHandshakeResponse
                {
                    HandshakeData = handshakeRequest.HandshakeData,
                    LastKnownApex = lastKnownApex
                });
            else //send a regular handshake response for a client
                await connection.SendMessage(new HandshakeResponse { HandshakeData = handshakeRequest.HandshakeData });
        }
    }
}
