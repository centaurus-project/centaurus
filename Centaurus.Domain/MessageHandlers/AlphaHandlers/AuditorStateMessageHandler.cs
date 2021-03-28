﻿using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain.MessageHandlers.AlphaHandlers
{
    public class AuditorStateMessageHandler : BaseAlphaMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorState;

        public override bool IsAuditorOnly { get; } = true;

        public override ConnectionState[] ValidConnectionStates { get; } = 
            new ConnectionState[] {
                ConnectionState.Validated,
                ConnectionState.Ready
            };

        public override async Task HandleMessage(AlphaWebSocketConnection connection, IncomingMessage message)
        {
            await AlphaCatchup.AddAuditorState(connection.ClientPubKey, (AuditorState)message.Envelope.Message);
        }
    }
}
