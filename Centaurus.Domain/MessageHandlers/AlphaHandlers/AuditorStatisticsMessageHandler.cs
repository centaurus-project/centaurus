using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AuditorPerfStatisticsMessageHandler : BaseAlphaMessageHandler
    {
        public AuditorPerfStatisticsMessageHandler(AlphaContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorPerfStatistics;

        public override ConnectionState[] ValidConnectionStates { get; } = null;

        public override bool IsAuditorOnly => true;

        public override Task HandleMessage(AlphaWebSocketConnection connection, IncomingMessage message)
        {
            var auditor = connection.ClientKPAccountId;
            var statistics = (AuditorPerfStatistics)message.Envelope.Message;
            var alphaPerfManager = (AlphaPerformanceStatisticsManager)Context.PerformanceStatisticsManager;
            _ = Task.Factory.StartNew(() => alphaPerfManager.AddAuditorStatistics(auditor, statistics));
            return Task.CompletedTask;
        }
    }
}