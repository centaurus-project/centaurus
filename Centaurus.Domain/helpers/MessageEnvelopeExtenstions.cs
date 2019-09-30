using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
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
            return messageEnvelope.Message.ComputeHash();
        }

        /// <summary>
        /// Signs an envelope with a given <see cref="KeyPair"/> and appends the signature to the <see cref="MessageEnvelope.Signatures"/>.
        /// </summary>
        /// <param name="messageEnvelope">Envelope to sign</param>
        /// <param name="keyPair">Key pair to use for signing</param>
        public static void Sign(this MessageEnvelope messageEnvelope, KeyPair keyPair)
        {
            var rawSignature = keyPair.Sign(messageEnvelope.ComputeMessageHash());

            var signature = new Ed25519Signature()
            {
                Signature = rawSignature,
                Signer = keyPair.PublicKey
            };
            messageEnvelope.Signatures.Add(signature);
        }

        /// <summary>
        /// Verifies that both aggregated envelops contain the same message and merges signatures to the source envelope.
        /// </summary>
        /// <param name="envelope">Source envelope</param>
        /// <param name="anotherEnvelope">Envelope to aggregate</param>
        public static void AggregateEnvelop(this MessageEnvelope envelope, MessageEnvelope anotherEnvelope)
        {
            //TODO: cache hashes to improve performance
            var hash = envelope.ComputeMessageHash();
            if (!ByteArrayPrimitives.Equals(anotherEnvelope.ComputeMessageHash(), hash))
                throw new InvalidOperationException($"Invalid message envelope hash. {hash} != {anotherEnvelope.ComputeMessageHash()}");
            foreach (var signature in anotherEnvelope.Signatures)
            {
                if (!envelope.Signatures.Contains(signature))
                {
                    //TODO: the signature has been checked on arrival, but there may be some edge cases when we need to check it regardless
                    envelope.Signatures.Add(signature);
                }
            }
        }

        /// <summary>
        /// Converts a list of envelops to a single envelope with aggregated signatures.
        /// </summary>
        /// <param name="envelopes">List of envelops to aggregate.</param>
        /// <returns>Aggregated envelope.</returns>
        public static MessageEnvelope AggregateEnvelops(this List<MessageEnvelope> envelopes)
        {
            var res = new MessageEnvelope
            {
                Message = envelopes[0].Message,
                Signatures = new List<Ed25519Signature>()
            };
            foreach (var msg in envelopes)
            {
                AggregateEnvelop(res, msg);
            }
            return res;
        }

        /// <summary>
        /// Checks that all envelope signatures are valid
        /// </summary>
        /// <param name="envelope">Target envelope</param>
        /// <returns>True if all signatures valid, otherwise false</returns>
        public static bool AreSignaturesValid(this MessageEnvelope envelope)
        {
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
            return envelope.Signatures.Any(s => s.Signer.Equals(pubKey));
        }

        public static ResultMessage CreateResult(this MessageEnvelope envelope, ResultStatusCodes status = ResultStatusCodes.InternalError, List<Effect> effects = null)
        {
            return new ResultMessage
            {
                OriginalMessage = envelope,
                Status = status,
                Effects = effects ?? new List<Effect>()
            };
        }
    }
}
