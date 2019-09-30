using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Test.Client
{
    public class ResultMessageHandler : BaseClientMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.ResultMessage;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] {
            ConnectionState.Ready,
            ConnectionState.Validated,
            ConnectionState.Connected
        };

        public override Task HandleMessage(UserWebSocketConnection connection, MessageEnvelope messageEnvelope)
        {
            if (connection.ConnectionState != ConnectionState.Ready)
            {
                //handle handshake confirmation from the alpha server
                var statusCode = GetStatusCode(messageEnvelope);
                if (statusCode != ResultStatusCodes.Success)
                {
                    var description = DescriptionAttributeReader.GetDescription(statusCode);
                    var error = $"Handshake failed. {statusCode} : {description}";
                    throw new ConnectionCloseException(WebSocketCloseStatus.ProtocolError, error);
                }
                else
                {
                    connection.SetConnectionState(ConnectionState.Ready);
                }
                return Task.CompletedTask;
            }

            var messageId = messageEnvelope.Message.MessageId;
            if (messageId > 0 && connection.Requests.TryRemove(messageId, out TaskCompletionSource<MessageEnvelope> req))
                req.SetResult(messageEnvelope);

            return Task.CompletedTask;
        }

        private ResultStatusCodes GetStatusCode(MessageEnvelope messageEnvelope)
        {
            var statusCode = ResultStatusCodes.InternalError;
            var result = messageEnvelope?.Message as ResultMessage;
            if (result != null)
                statusCode = result.Status;
            return statusCode;
        }
    }
}
