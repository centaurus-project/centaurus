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

            var alphaStateManager = (AlphaStateManager)Global.AppState;
            Message resultMessage;
            //if current client is auditor, then we should send current state to it. 
            //We set Ready state to an auditor only after the auditor sets apex cursor
            if (Global.Constellation.Auditors.Contains(connection.ClientPubKey))
            {
                resultMessage = alphaStateManager.GetCurrentAlphaState();
            }
            else
            {
                if (Global.AppState.State != ApplicationState.Ready)
                    throw new ConnectionCloseException(WebSocketCloseStatus.ProtocolError, "Alpha is not in Ready state.");
                connection.ConnectionState = ConnectionState.Ready;
                resultMessage = envelope.CreateResult(ResultStatusCodes.Success);
            }
            await connection.SendMessage(resultMessage);
        }
    }
}
