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

        public override async Task HandleMessage(BaseWebSocketConnection connection, IncomingMessage message)
        {
            var handshakeRequest = (HandshakeRequest)message.Envelope.Message;
            await connection.SendMessage(new HandshakeResponse { HandshakeData = handshakeRequest.HandshakeData }.CreateEnvelope());
        }
    }
}
