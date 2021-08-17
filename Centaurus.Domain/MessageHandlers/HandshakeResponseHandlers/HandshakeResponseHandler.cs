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
        public HandshakeResponseHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(HandshakeResponse).Name;

        public override async Task HandleMessage(IncomingClientConnection connection, IncomingMessage message)
        {
            await base.HandleMessage(connection, message);

            if (connection.Context.StateManager.State != State.Ready)
                throw new ConnectionCloseException(WebSocketCloseStatus.ProtocolError, "Alpha is not in Ready state.");

            if (connection.Account == null)
                throw new UnauthorizedException();

            var result = (ClientConnectionSuccess)message.Envelope.CreateResult(ResultStatusCodes.Success);
            result.AccountId = connection.Account.Id;
            await connection.SendMessage(result);
        }
    }
}
