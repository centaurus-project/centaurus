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
            {
                if (Global.AppState.State == ApplicationState.Rising)
                {
                    var payload = handshakeInit.Payload as AuditorHandshakePayload;
                    if (payload == null)
                        throw new ConnectionCloseException(WebSocketCloseStatus.InvalidPayloadData, "No auditor payload data.");
                    await AlphaCatchup.SetApex(connection.ClientPubKey, payload.Apex);
                }
                else
                {
                    var alphaStateManager = (AlphaStateManager)Global.AppState;
                    var stateMessage = await alphaStateManager.GetCurrentAlphaState();
                    await connection.SendMessage(stateMessage);
                }
            }
            else
            {
                if (Global.AppState.State != ApplicationState.Ready)
                    throw new ConnectionCloseException(WebSocketCloseStatus.ProtocolError, "Alpha is not in Ready state.");
                connection.ConnectionState = ConnectionState.Ready;
                await connection.SendMessage(envelope.CreateResult(ResultStatusCodes.Success));
            }
        }
    }
}
