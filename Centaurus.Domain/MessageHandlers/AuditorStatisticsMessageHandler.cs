using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public override string SupportedMessageType { get; } = typeof(AuditorPerfStatistics).Name;

        public override ConnectionState[] ValidConnectionStates => new[] { ConnectionState.Ready };

        public override bool IsAuditorOnly => true;

        public override Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            var auditor = connection.PubKeyAddress;
            var statistics = (AuditorPerfStatistics)message.Envelope.Message;

            Context.PerformanceStatisticsManager.AddAuditorStatistics(auditor, statistics);
            return Task.CompletedTask;
        }
    }
}