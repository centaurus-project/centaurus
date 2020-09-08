using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class ClientRequestProcessingHelper
    {
        public static void UpdateNonce<T>(this T context)
            where T : ProcessorContext
        {
            var requestQuantum = (RequestQuantum)context.Envelope.Message;
            var requestMessage = requestQuantum.RequestMessage;

            var currentUser = requestMessage.AccountWrapper.Account;

            context.EffectProcessors.AddNonceUpdate(currentUser, requestMessage.Nonce, currentUser.Nonce);
        }

        public static void ValidateNonce<T>(this T context)
            where T : ProcessorContext
        {
            var requestQuantum = context.Envelope.Message as RequestQuantum;
            if (requestQuantum == null)
                throw new InvalidOperationException($"Invalid message type. Client quantum message should be of type {typeof(RequestQuantum).Name}.");

            var requestMessage = requestQuantum.RequestEnvelope.Message as NonceRequestMessage;
            if (requestMessage == null)
                throw new InvalidOperationException($"Invalid message type. {typeof(RequestQuantum).Name} should contain message of type {typeof(NonceRequestMessage).Name}.");

            var currentUser = requestMessage.AccountWrapper.Account;
            if (currentUser == null)
                throw new Exception($"Account with public key '{requestMessage.ToString()}' is not found.");

            if (requestMessage.Nonce < 1 || currentUser.Nonce >= requestMessage.Nonce)
                throw new UnauthorizedException();
        }
    }
}
