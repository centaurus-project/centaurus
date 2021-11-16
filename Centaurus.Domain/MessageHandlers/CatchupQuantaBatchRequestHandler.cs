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
            while (true)
            {
                var currentBatch = Context.SyncStorage.GetQuanta(aboveApex, batchSize);
                if (currentBatch.Count < 1 && aboveApex < Context.QuantumHandler.CurrentApex)
                    throw new Exception("No quanta from database.");

                var signaturesBatch = Context.SyncStorage.GetSignatures(aboveApex, batchSize).ToDictionary(s => s.Apex, s => s.Signatures);

                var batch = new CatchupQuantaBatch
                {
                    Quanta = new List<CatchupQuantaBatchItem>(),
                    HasMore = currentBatch.Count == batchSize
                };

                foreach (var quantum in currentBatch)
                {
                    var apex = ((Quantum)quantum.Quantum).Apex;

                    var quantumSignatures = default(List<AuditorSignatureInternal>);
                    //if quantum was persisted, than it contains majority signatures already. Otherwise we need to obtain it from the result manager
                    if (apex > Context.PendingUpdatesManager.LastSavedApex)
                        Context.ResultManager.TryGetSignatures(apex, out quantumSignatures);
                    else
                    {
                        var alphaSignature = quantum.AlphaSignature;
                        if (signaturesBatch.TryGetValue(apex, out quantumSignatures))
                            quantumSignatures.Insert(0, quantum.AlphaSignature);
                        else
                            quantumSignatures = new List<AuditorSignatureInternal> { quantum.AlphaSignature };
                    }
                    if (quantumSignatures.Count < 1)
                    {
                        Context.StateManager.Failed(new Exception($"Unable to find signatures for quantum {apex}"));
                        return;
                    }
                    batch.Quanta.Add(new CatchupQuantaBatchItem
                    {
                        Quantum = quantum.Quantum,
                        Signatures = quantumSignatures
                    });
                    aboveApex = apex;
                }
                await connection.SendMessage(batch.CreateEnvelope<MessageEnvelopeSignless>());
                if (!batch.HasMore)
                    break;
            };
        }
    }
}
