using Centaurus.Domain;
using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Centaurus
{
    public static class MessageEnvelopeExtenstions
    {
        /// <summary>
        /// Compute SHA256 hash for the wrapped message.
        /// </summary>
        /// <param name="messageEnvelope">Envelope</param>
        /// <returns>Message hash</returns>
        public static byte[] ComputeMessageHash(this MessageEnvelope messageEnvelope)
        {
            if (messageEnvelope == null)
                throw new ArgumentNullException(nameof(messageEnvelope));
            return messageEnvelope.Message.ComputeHash();
        }

        /// <summary>
        /// Signs an envelope with a given <see cref="KeyPair"/> and appends the signature to the <see cref="MessageEnvelope.Signatures"/>.
        /// </summary>
        /// <param name="messageEnvelope">Envelope to sign</param>
        /// <param name="keyPair">Key pair to use for signing</param>
        public static MessageEnvelope Sign(this MessageEnvelope messageEnvelope, KeyPair keyPair)
        {
            if (messageEnvelope == null)
                throw new ArgumentNullException(nameof(messageEnvelope));
            if (keyPair == null)
                throw new ArgumentNullException(nameof(keyPair));
            var signature = messageEnvelope.ComputeMessageHash().Sign(keyPair);
            messageEnvelope.Signatures.Add(signature);
            return messageEnvelope;
        }

        /// <summary>
        /// Verifies that both aggregated envelops contain the same message and merges signatures to the source envelope.
        /// </summary>
        /// <param name="envelope">Source envelope</param>
        /// <param name="anotherEnvelope">Envelope to aggregate</param>
        public static void AggregateEnvelop(this MessageEnvelope envelope, MessageEnvelope anotherEnvelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            if (anotherEnvelope == null)
                throw new ArgumentNullException(nameof(anotherEnvelope));
            //TODO: cache hashes to improve performance
            var hash = envelope.ComputeMessageHash();
            var anotherHash = anotherEnvelope.ComputeMessageHash();
            if (!ByteArrayPrimitives.Equals(anotherHash, hash))
            {
#if DEBUG
                var envelopeJson = Newtonsoft.Json.JsonConvert.SerializeObject(envelope);
                Debug.Print("envelope:\n" + envelopeJson);
                envelopeJson = Newtonsoft.Json.JsonConvert.SerializeObject(anotherEnvelope);
                Debug.Print("anotherEnvelope:\n" + envelopeJson);
#endif

                throw new InvalidOperationException($"Invalid message envelope hash. {hash} != {anotherHash}");
            }
            envelope.AggregateEnvelopUnsafe(anotherEnvelope);
        }

        /// <summary>
        /// Merges signatures to the source envelope. 
        /// THIS METHOD DOESN'T VERIFY THAT ENVELOPS CONTAIN THE SAME MESSAGE!!
        /// </summary>
        /// <param name="envelope">Source envelope</param>
        /// <param name="anotherEnvelope">Envelope to aggregate</param>
        public static void AggregateEnvelopUnsafe(this MessageEnvelope envelope, MessageEnvelope anotherEnvelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            if (anotherEnvelope == null)
                throw new ArgumentNullException(nameof(anotherEnvelope));
            foreach (var signature in anotherEnvelope.Signatures)
            {
                if (!envelope.Signatures.Contains(signature))
                {
                    //TODO: the signature has been checked on arrival, but there may be some edge cases when we need to check it regardless
                    envelope.Signatures.Add(signature);
                }
            }
            var resultMessage = envelope.Message as ITransactionResultMessage;
            var anotherResultMessage = anotherEnvelope.Message as ITransactionResultMessage;
            if (resultMessage is null != anotherResultMessage is null)
                throw new Exception("Result types conflict");

            if (resultMessage != null)
                resultMessage.TxSignatures.AddRange(anotherResultMessage.TxSignatures);
        }

        /// <summary>
        /// Checks that all envelope signatures are valid
        /// </summary>
        /// <param name="envelope">Target envelope</param>
        /// <returns>True if all signatures valid, otherwise false</returns>
        public static bool AreSignaturesValid(this MessageEnvelope envelope)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            var messageHash = envelope.Message.ComputeHash();
            for (var i = 0; i < envelope.Signatures.Count; i++)
            {
                if (!envelope.Signatures[i].IsValid(messageHash))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Checks that envelope is signed with specified key
        /// !!!This method doesn't validate signature
        /// </summary>
        /// <param name="envelope">Target envelope</param>
        /// <param name="pubKey">Required signer public key</param>
        /// <returns>True if signed, otherwise false</returns>
        public static bool IsSignedBy(this MessageEnvelope envelope, RawPubKey pubKey)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            if (pubKey == null)
                throw new ArgumentNullException(nameof(pubKey));
            return envelope.Signatures.Any(s => s.Signer.Equals(pubKey));
        }

        private static TResultMessage CreateResult<TResultMessage>(this MessageEnvelope envelope, ResultStatusCodes status = ResultStatusCodes.InternalError, List<Effect> effects = null)
            where TResultMessage : ResultMessage
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            var resultMessage = Activator.CreateInstance<TResultMessage>();
            resultMessage.OriginalMessage = envelope;
            resultMessage.Status = status;
            resultMessage.Effects = effects ?? new List<Effect>();
            return resultMessage;
        }

        public static ResultMessage CreateResult(this MessageEnvelope envelope, ResultStatusCodes status = ResultStatusCodes.InternalError, List<Effect> effects = null)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            var messageType = envelope.Message.MessageType;
            if (envelope.Message is RequestQuantum)
                messageType = ((RequestQuantum)envelope.Message).RequestEnvelope.Message.MessageType;

            switch (messageType)
            {
                case MessageTypes.HandshakeInit:
                    return CreateResult<HandshakeResult>(envelope, status, effects);
                case MessageTypes.AccountDataRequest:
                    return CreateResult<AccountDataResponse>(envelope, status, effects);
                case MessageTypes.WithdrawalRequest:
                    return CreateResult<ITransactionResultMessage>(envelope, status, effects);
                default:
                    return CreateResult<ResultMessage>(envelope, status, effects);
            }
        }

        public static ResultMessage CreateResult(this MessageEnvelope envelope, Exception exc)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            if (exc == null)
                throw new ArgumentNullException(nameof(exc));
            var result = envelope.CreateResult(exc.GetStatusCode());
            if (!(result.Status == ResultStatusCodes.InternalError || string.IsNullOrWhiteSpace(exc.Message)))
                result.ErrorMessage = exc.Message;
            else
                result.ErrorMessage = result.Status.ToString();
            return result;
        }
    }
}
