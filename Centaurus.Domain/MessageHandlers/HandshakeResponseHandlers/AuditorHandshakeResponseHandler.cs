using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditorHandshakeResponseHandler : HandshakeResponseHandlerBase<IncomingAuditorConnection>
    {
        public AuditorHandshakeResponseHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorHandshakeResponse;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override bool IsAuditorOnly => true;

        public override async Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            await base.HandleMessage(connection, message);

            var auditorHandshake = (AuditorHandshakeResponse)message.Envelope.Message;

            if (!(connection is IncomingAuditorConnection incomingAuditorConnection))
                throw new BadRequestException("Invalid message.");

            if (!incomingAuditorConnection.TryValidate(auditorHandshake.HandshakeData))
                throw new ConnectionCloseException(WebSocketCloseStatus.InvalidPayloadData, "Handshake data is invalid.");

            incomingAuditorConnection.SetApexCursor(auditorHandshake.LastKnownApex);
        }
    }
}