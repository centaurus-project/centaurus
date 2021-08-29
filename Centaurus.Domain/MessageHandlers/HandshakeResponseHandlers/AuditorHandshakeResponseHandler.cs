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

            //send current state
            await incomingAuditorConnection.SendMessage(new StateUpdateMessage
            {
                State = Context.StateManager.State
            }.CreateEnvelope<MessageEnvelopeSignless>());

            Context.StateManager.SetAuditorState(connection.PubKey, auditorHandshake.State);
            incomingAuditorConnection.SetSyncCursor(auditorHandshake.QuantaCursor, auditorHandshake.ResultCursor);

            //request quanta on rising
            if (Context.StateManager.State == State.Rising)
            {
                await incomingAuditorConnection.SendMessage(new QuantaBatchRequest
                {
                    QuantaCursor = Context.QuantumStorage.CurrentApex
                }.CreateEnvelope<MessageEnvelopeSignless>());
            }
        }
    }
}