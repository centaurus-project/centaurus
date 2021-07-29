using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class HandshakeResponseHandler : HandshakeResponseHandlerBase<IncomingClientConnection>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public HandshakeResponseHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.HandshakeResponse;

        public override async Task HandleMessage(IncomingClientConnection connection, IncomingMessage message)
        {
            await base.HandleMessage(connection, message);

            if (connection.Context.AppState.State != State.Ready)
                throw new ConnectionCloseException(WebSocketCloseStatus.ProtocolError, "Alpha is not in Ready state.");
            var result = (ClientConnectionSuccess)message.Envelope.CreateResult(ResultStatusCodes.Success);
            await connection.SendMessage(result);
        }
    }
}
