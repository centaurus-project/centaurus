using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AuditorPerfStatisticsMessageHandler : MessageHandlerBase
    {
        public AuditorPerfStatisticsMessageHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorPerfStatistics;

        public override bool IsAuditorOnly => true;

        public override Task HandleMessage(BaseWebSocketConnection connection, IncomingMessage message)
        {
            var auditor = connection.PubKeyAddress;
            var statistics = (AuditorPerfStatistics)message.Envelope.Message;
            _ = Task.Factory.StartNew(() => Context.PerformanceStatisticsManager.AddAuditorStatistics(auditor, statistics));
            return Task.CompletedTask;
        }
    }
}