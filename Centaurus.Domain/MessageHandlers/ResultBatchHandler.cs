using Centaurus.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class ResultBatchHandler : MessageHandlerBase
    {
        public ResultBatchHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(AuditorSignaturesBatch).Name;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override bool IsAuditorOnly { get; } = true;

        public override Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            var resultsBatch = (AuditorSignaturesBatch)message.Envelope.Message;
            foreach (var result in resultsBatch.AuditorResultMessages)
                Context.ResultManager.Add(new QuantumSignatures
                {
                    Apex = result.Apex,
                    Signatures = new List<AuditorSignatureInternal> { result.Signature }
                });
            return Task.CompletedTask;
        }
    }
}