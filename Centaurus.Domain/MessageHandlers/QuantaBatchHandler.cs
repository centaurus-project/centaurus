using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using NLog;

namespace Centaurus.Domain
{
    public class QuantaBatchHandler : MessageHandlerBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public QuantaBatchHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override bool IsAuditorOnly => true;

        public override string SupportedMessageType { get; } = typeof(QuantaBatch).Name;

        public override ConnectionState[] ValidConnectionStates => new ConnectionState[] { ConnectionState.Validated, ConnectionState.Ready };

        public override async Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            if (Context.StateManager.State == State.Rising)
                await AddAuditorState(connection, message);
            else
                await AddQuantaToQueue(connection, message);
        }

        private async Task AddQuantaToQueue(ConnectionBase connection, IncomingMessage message)
        {
            var quantumHandler = connection.Context.QuantumHandler;
            var quantaBatch = (QuantaBatch)message.Envelope.Message;
            var quanta = quantaBatch.Quanta;
            foreach (var processedQuantum in quanta)
            {
                var quantum = processedQuantum.Quantum;
                if (quantum.Apex <= quantumHandler.LastAddedQuantumApex)
                    continue;

                if (quantum.Apex != quantumHandler.LastAddedQuantumApex + 1)
                {
                    logger.Warn($"Batch has invalid quantum apexes (current: {quantumHandler.LastAddedQuantumApex}, received: {quantum.Apex}). New apex cursor request will be send.");
                    await connection.SendMessage(new QuantaBatchRequest { LastKnownApex = quantumHandler.LastAddedQuantumApex });
                    return;
                }
                _ = Context.QuantumHandler.HandleAsync(processedQuantum.Quantum);
            }
        }

        private async Task AddAuditorState(ConnectionBase connection, IncomingMessage message)
        {
            await Context.Catchup.AddAuditorState(connection.PubKey, message.Envelope.Message as QuantaBatch);
        }
    }
}
