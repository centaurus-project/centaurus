using Centaurus.Models;
using NLog;
using System.Collections.Generic;
using System.Diagnostics;
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

            //update alpha apex
            Context.StateManager.UpdateAlphaApex(quantaBatch.LastKnownApex);

            var quanta = quantaBatch.Quanta;
            foreach (var processedQuantum in quanta)
            {
                var quantum = (Quantum)processedQuantum;
                //get last known apex
                var lastKnownApex = quantumHandler.LastAddedQuantumApex == 0 ? Context.QuantumStorage.CurrentApex : quantumHandler.LastAddedQuantumApex;
                if (quantum.Apex <= lastKnownApex)
                    continue;

                if (quantum.Apex != lastKnownApex + 1)
                {
                    await connection.SendMessage(new QuantaBatchRequest
                    {
                        QuantaCursor = quantumHandler.LastAddedQuantumApex,
                        ResultCursor = Context.RoleManager.ParticipationLevel == CentaurusNodeParticipationLevel.Prime ? ulong.MaxValue : Context.PendingUpdatesManager.LastSavedApex //prime servers will receive results from other auditors
                    }.CreateEnvelope<MessageEnvelopeSignless>());
                    logger.Warn($"Batch has invalid quantum apexes (current: {quantumHandler.LastAddedQuantumApex}, received: {quantum.Apex}). New apex cursor request sent.");
                    return;
                }

                _ = Context.QuantumHandler.HandleAsync(quantum);
            }
            var signatures = quantaBatch.Signatures;
            if (signatures != null)
                foreach (var apexSignatures in signatures)
                    foreach (var signature in apexSignatures.Signatures)
                        Context.ResultManager.Add(new AuditorResult { Apex = apexSignatures.Apex, Signature = signature });
        }

        private async Task AddAuditorState(ConnectionBase connection, IncomingMessage message)
        {
            await Context.Catchup.AddAuditorState(connection.PubKey, message.Envelope.Message as QuantaBatch);
        }
    }
}
