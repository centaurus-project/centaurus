using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Domain;
using Centaurus.Models;

namespace Centaurus.Test.Client
{
    public class AuditorHandeshakeHandler : BaseClientMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.HandshakeInit;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override async Task HandleMessage(UserWebSocketConnection connection, MessageEnvelope envelope)
        {
            await connection.SendMessage(envelope.Message);
        }
    }
}
