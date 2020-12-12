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

        public override MessageTypes SupportedMessageType => MessageTypes.QuantaBatch;

        public override ConnectionState[] ValidConnectionStates => new ConnectionState[] { ConnectionState.Connected, ConnectionState.Ready };

        public override async Task HandleMessage(AuditorWebSocketConnection connection, MessageEnvelope messageEnvelope)
        {
            var quantaBatch = (QuantaBatch)messageEnvelope.Message;
            var quanta = quantaBatch.Quanta;
            var quantaBatchCount = quanta.Count;
            for (var i = 0; i < quantaBatchCount; i++)
            {
                var quantum = (Quantum)quanta[i].Message;
                if (quantum.Apex <= Global.QuantumHandler.LastAddedQuantumApex)
                    continue;

                if (quantum.Apex != Global.QuantumHandler.LastAddedQuantumApex + 1)
                {
                    logger.Info($"Batch has invalid quantum apexes (current: {Global.QuantumHandler.LastAddedQuantumApex}, received: {quantum.Apex}). New apex cursor request will be send.");
                    await connection.SendMessage(new SetApexCursor { Apex = Global.QuantumHandler.LastAddedQuantumApex });
                    return;
                }
                _ = Global.QuantumHandler.HandleAsync(quanta[i]);
            }
        }
    }
}
