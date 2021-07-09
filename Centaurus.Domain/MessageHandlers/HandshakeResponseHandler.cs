using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class HandshakeResponseHandler : MessageHandlerBase
    {
        public HandshakeResponseHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.HandshakeResponse;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };


        public override async Task HandleMessage(BaseWebSocketConnection connection, IncomingMessage message)
        {
            var handshakeInit = message.Envelope.Message as HandshakeResponse;
            if (!ByteArrayPrimitives.Equals(handshakeInit.HandshakeData.Data, connection.HandshakeData.Data))
                throw new ConnectionCloseException(WebSocketCloseStatus.InvalidPayloadData, "Handshake data is invalid.");

            connection.SetPubKey(message.Envelope.Signatures[0].Signer);

            if (connection.IsAuditor)
                await HandleAuditorHandshake(connection);
            else
                await HandleClientHandshake(connection, message.Envelope);

        }

        private async Task HandleAuditorHandshake(BaseWebSocketConnection connection)
        {
            Message message;
            if (connection.Context.AppState.State == ApplicationState.Rising)
                message = new AuditorStateRequest { TargetApex = connection.Context.PersistenceManager.GetLastApex() };
            else
                message = connection.Context.GetCurrentState();
            await connection.SendMessage(message);
        }

        private async Task HandleClientHandshake(BaseWebSocketConnection connection, MessageEnvelope envelope)
        {
            if (connection.Context.AppState.State != ApplicationState.Ready)
                throw new ConnectionCloseException(WebSocketCloseStatus.ProtocolError, "Alpha is not in Ready state.");
            var result = (ClientConnectionSuccess)envelope.CreateResult(ResultStatusCodes.Success);
            await connection.SendMessage(result);
        }
    }
}
