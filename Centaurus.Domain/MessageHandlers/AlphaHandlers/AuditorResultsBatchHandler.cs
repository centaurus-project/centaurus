using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain.MessageHandlers.AlphaHandlers
{
    public class AuditorResultsBatchHandler : BaseAlphaMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorResultsBatch;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override bool IsAuditorOnly { get; } = true;

        //TODO: run result aggregation in separate thread
        public override Task HandleMessage(AlphaWebSocketConnection connection, IncomingMessage message)
        {
            var resultsBatch = (AuditorResultsBatch)message.Envelope.Message;
            foreach (var result in resultsBatch.AuditorResultMessages)
                connection.AlphaContext.AuditResultManager.Add(result, connection.ClientPubKey);
            return Task.CompletedTask;
        }
    }
}