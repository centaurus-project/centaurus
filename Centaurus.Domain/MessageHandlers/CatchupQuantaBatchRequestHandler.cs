using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class CatchupQuantaBatchRequestHandler : MessageHandlerBase<OutgoingConnection>
    {
        public CatchupQuantaBatchRequestHandler(ExecutionContext context)
            : base(context)
        {

        }
        public override string SupportedMessageType => typeof(CatchupQuantaBatchRequest).Name;

        public override async Task HandleMessage(OutgoingConnection connection, IncomingMessage message)
        {
            var batchRequest = (CatchupQuantaBatchRequest)message.Envelope.Message;
            await SendQuanta(connection, batchRequest);
        }

        private async Task SendQuanta(OutgoingConnection connection, CatchupQuantaBatchRequest batchRequest)
        {
            var aboveApex = batchRequest.LastKnownApex;
            var batchSize = Context.Settings.SyncBatchSize;
            if (aboveApex < Context.QuantumStorage.CurrentApex)
                while (aboveApex < Context.QuantumStorage.CurrentApex)
                {
                    var currentBatch = Context.QuantumStorage.GetQuanta(aboveApex, batchSize);
                    if (currentBatch.Count < 1)
                        throw new Exception("No quanta from database.");

                    var batch = new CatchupQuantaBatch
                    {
                        Quanta = new List<CatchupQuantaBatchItem>(),
                        HasMore = currentBatch.Count == batchSize
                    };

                    foreach (var quantum in currentBatch)
                    {
                        var signatures = quantum.Signatures;
                        //if quantum was persisted, than it contains signatures already. Otherwise we need to obtain it from the result manager
                        if (quantum.Quantum.Apex > Context.PendingUpdatesManager.LastSavedApex)
                            Context.ResultManager.TryGetSignatures(quantum.Quantum.Apex, out signatures);
                        //quantum must contain at least two signatures, Alpha and own one
                        if (signatures.Count < 2)
                        {
                            Context.StateManager.Failed(new Exception($"Unable to find signatures for quantum {quantum.Quantum.Apex}"));
                            return;
                        }
                        batch.Quanta.Add(new CatchupQuantaBatchItem
                        {
                            Quantum = quantum.Quantum,
                            Signatures = signatures
                        });
                    }

                    await connection.SendMessage(batch.CreateEnvelope<MessageEnvelopeSignless>());
                    aboveApex = currentBatch.Last().Quantum.Apex;
                }
            else
                await connection.SendMessage(new CatchupQuantaBatch
                {
                    Quanta = new List<CatchupQuantaBatchItem>(),
                    HasMore = false
                }.CreateEnvelope<MessageEnvelopeSignless>());
        }
    }
}
