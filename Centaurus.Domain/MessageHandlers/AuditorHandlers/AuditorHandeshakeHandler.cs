using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AuditorHandeshakeHandler : BaseAuditorMessageHandler
    {
        public AuditorHandeshakeHandler(AuditorContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.HandshakeInit;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };

        public override async Task HandleMessage(AuditorWebSocketConnection connection, IncomingMessage message)
        {
            //send message back. The message contains handshake data
            await connection.SendMessage(message.Envelope.Message);
        }
    }
}
