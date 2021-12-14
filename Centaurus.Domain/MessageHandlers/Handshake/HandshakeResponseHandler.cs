using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal class HandshakeResponseHandler : MessageHandlerBase<IncomingConnectionBase>
    {
        public HandshakeResponseHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override bool IsAuthenticatedOnly => false;

        public override string SupportedMessageType { get; } = typeof(HandshakeResponse).Name;

        public override async Task HandleMessage(IncomingConnectionBase connection, IncomingMessage message)
        {
            var handshakeResponse = message.Envelope.Message as HandshakeResponse;
            if (!connection.TryValidate(handshakeResponse.HandshakeData))
                throw new ConnectionCloseException(WebSocketCloseStatus.InvalidPayloadData, "Handshake data is invalid.");

            switch (connection)
            {
                case IncomingClientConnection clientConnection:
                    await HandleClientHandshake(clientConnection, message.Envelope);
                    break;
                case IncomingNodeConnection auditorConnection:
                    await HandleAuditorHandshake(auditorConnection);
                    break;
                default:
                    throw new BadRequestException("Unknown connection.");
            }
        }

        private async Task HandleClientHandshake(IncomingClientConnection clientConnection, MessageEnvelopeBase envelope)
        {
            //if Alpha is not ready, close connection
            if (clientConnection.Context.NodesManager.CurrentNode.State != State.Ready)
                throw new ConnectionCloseException(WebSocketCloseStatus.ProtocolError, "Server is not ready.");

            //if account not presented, throw UnauthorizedException
            if (clientConnection.Account == null)
                throw new UnauthorizedException();

            //send success response
            await clientConnection.SendMessage(envelope.CreateResult(ResultStatusCode.Success));
        }

        private async Task HandleAuditorHandshake(IncomingNodeConnection auditorConnection)
        {
            //register connection
            auditorConnection.Node.RegisterIncomingConnection(auditorConnection);

            //request quanta on rising
            if (Context.NodesManager.CurrentNode.State == State.Rising)
            {
                await auditorConnection.SendMessage(new CatchupQuantaBatchRequest
                {
                    LastKnownApex = Context.QuantumHandler.CurrentApex
                }.CreateEnvelope<MessageEnvelopeSignless>());
            }
        }
    }
}
