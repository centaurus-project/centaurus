using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Domain.Models;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public abstract class QuantumProcessorBase : ContextualBase
    {
        public QuantumProcessorBase(ExecutionContext context)
            : base(context)
        {

        }

        /// <summary>
        /// Short quantum class name
        /// </summary>
        public abstract string SupportedMessageType { get; }


        /// <summary>
        /// Execute quantum request and generate response message.
        /// </summary>
        /// <param name="context">Request context</param>
        public abstract Task<QuantumResultMessageBase> Process(QuantumProcessingItem context);

        /// <summary>
        /// Validate quantum request preconditions.
        /// </summary>
        /// <param name="context">Request context</param>
        public abstract Task Validate(QuantumProcessingItem context);


        public static void UpdateNonce(QuantumProcessingItem processingItem)
        {
            var requestQuantum = (RequestQuantumBase)processingItem.Quantum;
            var requestMessage = requestQuantum.RequestMessage;

            processingItem.AddNonceUpdate(processingItem.Initiator, requestMessage.Nonce, processingItem.Initiator.Nonce);
        }

        public static void ValidateNonce(QuantumProcessingItem processingItem)
        {
            var requestQuantum = processingItem.Quantum as RequestQuantumBase;
            if (requestQuantum == null)
                throw new BadRequestException($"Invalid message type. Client quantum message should be of type {typeof(RequestQuantumBase).Name}.");

            var requestMessage = requestQuantum.RequestEnvelope.Message as SequentialRequestMessage;
            if (requestMessage == null)
                throw new BadRequestException($"Invalid message type. {typeof(RequestQuantumBase).Name} should contain message of type {typeof(SequentialRequestMessage).Name}.");

            if (requestMessage.Nonce < 1 || processingItem.Initiator.Nonce >= requestMessage.Nonce)
                throw new UnauthorizedException($"Specified nonce is invalid. Current nonce: {processingItem.Initiator.Nonce}; request nonce: {requestMessage.Nonce}.");
        }
    }
}
