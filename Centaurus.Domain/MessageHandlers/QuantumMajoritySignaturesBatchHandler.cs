using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal class QuantumMajoritySignaturesBatchHandler : MessageHandlerBase<OutgoingConnection>
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public QuantumMajoritySignaturesBatchHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override bool IsAuditorOnly => true;

        public override string SupportedMessageType { get; } = typeof(QuantumMajoritySignaturesBatch).Name;

        public override ConnectionState[] ValidConnectionStates => new ConnectionState[] { ConnectionState.Ready };

        public override Task HandleMessage(OutgoingConnection connection, IncomingMessage message)
        {
            var signaturesBatch = (QuantumMajoritySignaturesBatch)message.Envelope.Message;
            foreach (var signatures in signaturesBatch.Signatures)
                Context.ResultManager.Add(signatures);
            return Task.CompletedTask;
        }
    }
}