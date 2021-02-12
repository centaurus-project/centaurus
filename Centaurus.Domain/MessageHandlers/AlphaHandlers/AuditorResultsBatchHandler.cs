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
        public override Task HandleMessage(AlphaWebSocketConnection connection, MessageEnvelope envelope)
        {
            var resultsBatch = (AuditorResultsBatch)envelope.Message;
            foreach (var result in resultsBatch.AuditorResultMessages)
                Global.AuditResultManager.Add(result, connection.ClientPubKey);
            return Task.CompletedTask;
        }
    }
}