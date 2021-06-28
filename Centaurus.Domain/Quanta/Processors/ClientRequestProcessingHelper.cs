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
            var requestQuantum = (RequestQuantum)context.Envelope.Message;
            var requestMessage = requestQuantum.RequestMessage;

            context.EffectProcessors.AddNonceUpdate(context.SourceAccount, requestMessage.Nonce, context.SourceAccount.Account.Nonce);
        }

        public static void ValidateNonce<T>(this T context)
            where T : RequestContext
        {
            var requestQuantum = context.Envelope.Message as RequestQuantum;
            if (requestQuantum == null)
                throw new BadRequestException($"Invalid message type. Client quantum message should be of type {typeof(RequestQuantum).Name}.");

            var requestMessage = requestQuantum.RequestEnvelope.Message as SequentialRequestMessage;
            if (requestMessage == null)
                throw new BadRequestException($"Invalid message type. {typeof(RequestQuantum).Name} should contain message of type {typeof(SequentialRequestMessage).Name}.");

            if (requestMessage.Nonce < 1 || context.SourceAccount.Account.Nonce >= requestMessage.Nonce)
                throw new UnauthorizedException($"Specified nonce is invalid. Current nonce: {context.SourceAccount.Account}; request nonce: {requestMessage.Nonce}.");
        }
    }
}
