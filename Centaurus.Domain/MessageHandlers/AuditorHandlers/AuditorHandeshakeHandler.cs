using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AuditorHandeshakeHandler : BaseAuditorMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.HandshakeInit;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override async Task HandleMessage(AuditorWebSocketConnection connection, MessageEnvelope envelope)
        {
            //add payload and send message back. The message contains handshake data
            var handshakeMessage = (HandshakeInit)envelope.Message;
            handshakeMessage.Payload = new AuditorHandshakePayload { Apex = await SnapshotManager.GetLastApex() };
            await connection.SendMessage(handshakeMessage);
        }
    }
}
