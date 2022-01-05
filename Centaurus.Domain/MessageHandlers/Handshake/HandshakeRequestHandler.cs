using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    internal class HandshakeRequestHandler : MessageHandlerBase<OutgoingConnection>
    {
        public HandshakeRequestHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(HandshakeRequest).Name;

        public override bool IsAuthenticatedOnly => false;

        public override async Task HandleMessage(OutgoingConnection connection, IncomingMessage message)
        {
            var handshakeRequest = (HandshakeRequest)message.Envelope.Message;
            await connection.SendMessage(new HandshakeResponse
            {
                HandshakeData = handshakeRequest.HandshakeData
            });

            //we know that we connect to auditor so we can mark connection as authenticated
            connection.HandshakeDataSend();
        }
    }
}
