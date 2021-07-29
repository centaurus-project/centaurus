using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class HandshakeResponseHandlerBase<T> : MessageHandlerBase<T>
        where T : IncomingConnectionBase
    {
        protected HandshakeResponseHandlerBase(ExecutionContext context) 
            : base(context)
        {
        }

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Connected };


        public override Task HandleMessage(T connection, IncomingMessage message)
        {
            var handshakeRequest = message.Envelope.Message as HandshakeResponseBase;
            if (!connection.TryValidate(handshakeRequest.HandshakeData))
                throw new ConnectionCloseException(WebSocketCloseStatus.InvalidPayloadData, "Handshake data is invalid.");

            return Task.CompletedTask;
        }
    }
}
