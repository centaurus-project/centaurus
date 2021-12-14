using Centaurus.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal class SingleNodeSignaturesBatchHandler : MessageHandlerBase
    {
        public SingleNodeSignaturesBatchHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(SingleNodeSignaturesBatch).Name;

        public override bool IsAuthenticatedOnly => true;

        public override bool IsAuditorOnly { get; } = true;

        public override Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            var resultsBatch = (SingleNodeSignaturesBatch)message.Envelope.Message;
            foreach (var result in resultsBatch.Items)
                Context.ResultManager.Add(new MajoritySignaturesBatchItem
                {
                    Apex = result.Apex,
                    Signatures = new List<NodeSignatureInternal> { result.Signature }
                });
            return Task.CompletedTask;
        }
    }
}