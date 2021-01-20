using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class AlphaHandshakeHandler : BaseAlphaMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.HandshakeInit;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override bool IsAuthRequired { get; } = false;

        public override bool IsAuditorOnly { get; } = false;

        public override async Task HandleMessage(AlphaWebSocketConnection connection, MessageEnvelope envelope)
        {
            var handshakeInit = envelope.Message as HandshakeInit;
            if (!ByteArrayPrimitives.Equals(handshakeInit.HandshakeData.Data, connection.HandshakeData.Data))
                throw new ConnectionCloseException(WebSocketCloseStatus.InvalidPayloadData, "Handshake failed");

            connection.ClientPubKey = envelope.Signatures[0].Signer;
            connection.ConnectionState = ConnectionState.Validated;

            if (Global.Constellation.Auditors.Contains(connection.ClientPubKey))
                await HandleAuditorHandshake(connection);
            else
                await HandleClientHandshake(connection, envelope);

        }

        private async Task HandleAuditorHandshake(AlphaWebSocketConnection connection)
        {
            Message message;
            if (Global.AppState.State == ApplicationState.Rising)
                message = new AuditorStateRequest { TargetApex = await Global.PersistenceManager.GetLastApex() };
            else
                message = AlphaStateHelper.GetCurrentState();
            await connection.SendMessage(message);
        }

        private async Task HandleClientHandshake(AlphaWebSocketConnection connection, MessageEnvelope envelope)
        {
            if (Global.AppState.State != ApplicationState.Ready)
                throw new ConnectionCloseException(WebSocketCloseStatus.ProtocolError, "Alpha is not in Ready state.");
            connection.Account = Global.AccountStorage.GetAccount(connection.ClientPubKey);
            if (connection.Account == null)
                throw new ConnectionCloseException(WebSocketCloseStatus.NormalClosure, "Account is not registered.");
            connection.ConnectionState = ConnectionState.Ready;
            var result = (HandshakeResult)envelope.CreateResult(ResultStatusCodes.Success);
            result.AccountId = connection.Account.Account.Id;
            await connection.SendMessage(result);
        }
    }
}
