using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using NLog;

namespace Centaurus.Domain
{
    public class QuantaBatchHandler : BaseAuditorMessageHandler
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public QuantaBatchHandler(ExecutionContext context) 
            : base(context)
        {
        }

        public override MessageTypes SupportedMessageType => MessageTypes.QuantaBatch;

        public override ConnectionState[] ValidConnectionStates => new ConnectionState[] { ConnectionState.Connected, ConnectionState.Ready };

        public override async Task HandleMessage(OutgoingWebSocketConnection connection, IncomingMessage message)
        {
            var quantumHandler = connection.Context.QuantumHandler;
            var quantaBatch = (QuantaBatch)message.Envelope.Message;
            var quanta = quantaBatch.Quanta;
            var quantaBatchCount = quanta.Count;
            for (var i = 0; i < quantaBatchCount; i++)
            {
                var quantumEnvelope = quanta[i];
                var quantum = (Quantum)quantumEnvelope.Message;
                if (quantum.Apex <= quantumHandler.LastAddedQuantumApex)
                    continue;

                if (quantum.Apex != quantumHandler.LastAddedQuantumApex + 1)
                {
                    logger.Warn($"Batch has invalid quantum apexes (current: {quantumHandler.LastAddedQuantumApex}, received: {quantum.Apex}). New apex cursor request will be send.");
                    await connection.SendMessage(new SetApexCursor { Apex = quantumHandler.LastAddedQuantumApex });
                    return;
                }
                _ = Context.QuantumHandler.HandleAsync(quantumEnvelope);
            }
        }
    }
}
