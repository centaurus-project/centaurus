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

        public override string SupportedMessageType { get; } = typeof(AuditorHandshakeResponse).Name;

        public override bool IsAuditorOnly => true;

        public override async Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            await base.HandleMessage(connection, message);

            var auditorHandshake = (AuditorHandshakeResponse)message.Envelope.Message;

            if (!(connection is IncomingAuditorConnection incomingAuditorConnection))
                throw new BadRequestException("Invalid message.");

            if (!incomingAuditorConnection.TryValidate(auditorHandshake.HandshakeData))
                throw new ConnectionCloseException(WebSocketCloseStatus.InvalidPayloadData, "Handshake data is invalid.");

            //if has any quanta than running otherwise init
            var auditorState = auditorHandshake.LastKnownApex == 0 ? State.Undefined : State.Running;

            //if auditor sent handshake response that it at least at Running state
            Context.StateManager.SetAuditorState(connection.PubKey, auditorState);

            incomingAuditorConnection.SetApexCursor(auditorHandshake.LastKnownApex);
        }
    }
}