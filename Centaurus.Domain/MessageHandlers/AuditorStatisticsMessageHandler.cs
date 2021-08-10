using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public class AuditorPerfStatisticsMessageHandler : MessageHandlerBase<IncomingAuditorConnection>
    {
        public AuditorPerfStatisticsMessageHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType { get; } = MessageTypes.AuditorPerfStatistics;

        public override ConnectionState[] ValidConnectionStates => new[] { ConnectionState.Validated, ConnectionState.Ready };

        public override bool IsAuditorOnly => true;

        public override Task HandleMessage(IncomingAuditorConnection connection, IncomingMessage message)
        {
            var auditor = connection.PubKeyAddress;
            var statistics = (AuditorPerfStatistics)message.Envelope.Message;

            Context.StateManager.SetAuditorState(connection.PubKey, statistics.State);

            Debug.WriteLine($"{Context.Settings.KeyPair.AccountId} : {connection.PubKey.GetAccountId()} — {statistics.State}");

            _ = Task.Factory.StartNew(() => Context.PerformanceStatisticsManager.AddAuditorStatistics(auditor, statistics));
            return Task.CompletedTask;
        }
    }
}