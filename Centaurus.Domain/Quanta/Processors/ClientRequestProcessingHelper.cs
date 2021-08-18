using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Centaurus.Domain
{
    public static class ClientRequestProcessingHelper
    {
        public static void UpdateNonce<T>(this T context)
            where T : RequestContext
        {
            var requestQuantum = (RequestQuantumBase)context.Quantum;
            var requestMessage = requestQuantum.RequestMessage;

            context.AddNonceUpdate(context.InitiatorAccount, requestMessage.Nonce, context.InitiatorAccount.Account.Nonce);
        }

        public static void ValidateNonce<T>(this T context)
            where T : RequestContext
        {
            var requestQuantum = context.Quantum as RequestQuantumBase;
            if (requestQuantum == null)
                throw new BadRequestException($"Invalid message type. Client quantum message should be of type {typeof(RequestQuantumBase).Name}.");

            var requestMessage = requestQuantum.RequestEnvelope.Message as SequentialRequestMessage;
            if (requestMessage == null)
                throw new BadRequestException($"Invalid message type. {typeof(RequestQuantumBase).Name} should contain message of type {typeof(SequentialRequestMessage).Name}.");

            if (requestMessage.Nonce < 1 || context.InitiatorAccount.Account.Nonce >= requestMessage.Nonce)
                throw new UnauthorizedException($"Specified nonce is invalid. Current nonce: {context.InitiatorAccount.Account}; request nonce: {requestMessage.Nonce}.");
        }
    }
}
