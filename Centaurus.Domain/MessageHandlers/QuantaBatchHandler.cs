using Centaurus.Models;
using NLog;
using System.Threading.Tasks;

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

        public override ConnectionState[] ValidConnectionStates => new ConnectionState[] { ConnectionState.Ready };

        public override async Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            if (Context.StateManager.State == State.Rising)
                await AddAuditorState(connection, message);
            else
                await AddQuantaToQueue(connection, message);
        }

        private async Task AddQuantaToQueue(ConnectionBase connection, IncomingMessage message)
        {
            var quantumHandler = Context.QuantumHandler;
            var quantaBatch = (QuantaBatch)message.Envelope.Message;
            var quanta = quantaBatch.Quanta;
            foreach (var processedQuantum in quanta)
            {
                var quantum = processedQuantum.Quantum;
                //get last known apex
                var lastKnownApex = quantumHandler.LastAddedQuantumApex == 0 ? Context.QuantumStorage.CurrentApex : quantumHandler.LastAddedQuantumApex;
                if (quantum.Apex <= lastKnownApex)
                    continue;

                if (quantum.Apex != lastKnownApex + 1)
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
