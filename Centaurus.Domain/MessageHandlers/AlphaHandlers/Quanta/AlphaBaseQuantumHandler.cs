using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    /// <summary>
    /// This handler should handle all quantum requests
    /// </summary>
    public abstract class AlphaBaseQuantumHandler : BaseAlphaMessageHandler
    {
        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override bool IsAuditorOnly { get; } = false;

        public override Task HandleMessage(AlphaWebSocketConnection connection, IncomingMessage message)
        {
            connection.Context.QuantumHandler.HandleAsync(message.Envelope);
            return Task.CompletedTask;
        }
    }
}
