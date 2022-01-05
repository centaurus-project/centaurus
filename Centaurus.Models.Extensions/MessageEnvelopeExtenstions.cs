﻿using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus
{
    public static class MessageEnvelopeExtenstions
    {
        /// <summary>
        /// Compute SHA256 hash for the wrapped message.
        /// </summary>
        /// <param name="messageEnvelope">Envelope</param>
        /// <param name="buffer">Buffer to use for serialization.</param>
        /// <returns>Message hash</returns>
        public static byte[] ComputeMessageHash(this MessageEnvelopeBase messageEnvelope, byte[] buffer = null)
        {
            if (messageEnvelope == null)
                throw new ArgumentNullException(nameof(messageEnvelope));
            return messageEnvelope.Message.ComputeHash(buffer);
        }

        /// <summary>
        /// Signs an envelope with a given <see cref="KeyPair"/> and appends the signature to the <see cref="MessageEnvelopeBase.Signature"/>.
        /// </summary>
        /// <param name="messageEnvelope">Envelope to sign</param>
        /// <param name="keyPair">Key pair to use for signing</param>
        /// <param name="buffer">Buffer to use for computing hash code.</param>
        public static T Sign<T>(this T messageEnvelope, KeyPair keyPair, byte[] buffer = null)
            where T : MessageEnvelopeBase
        {
            if (messageEnvelope == null)
                throw new ArgumentNullException(nameof(messageEnvelope));
            if (keyPair == null)
                throw new ArgumentNullException(nameof(keyPair));

            if (messageEnvelope is MessageEnvelopeSignless)
                return messageEnvelope;

            var writer = default(XdrBufferWriter);
            if (buffer == null)
                writer = new XdrBufferWriter();
            else
                writer = new XdrBufferWriter(buffer);

            XdrConverter.Serialize(messageEnvelope.Message, writer);
            var signature = writer.ToArray().ComputeHash().Sign(keyPair);
            if (messageEnvelope is MessageEnvelope envelope)
                envelope.Signature = signature;
            else if (messageEnvelope is ConstellationMessageEnvelope multisigEnvelope)
            {
                if (multisigEnvelope.Signatures == null)
                    multisigEnvelope.Signatures = new List<TinySignature>();
                multisigEnvelope.Signatures.Add(signature);
            }
            return messageEnvelope;
        }

        /// <summary>
        /// Checks that envelope signature is valid
        /// </summary>
        /// <param name="envelope">Target envelope</param>
        /// <returns>True if signature is valid, otherwise false</returns>
        public static bool IsSignatureValid(this MessageEnvelopeBase envelope, KeyPair keyPair, bool allowSignless = true)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            if (keyPair == null)
                throw new ArgumentNullException(nameof(keyPair));

            if (envelope is MessageEnvelopeSignless)
                return true;

            var messageHash = envelope.Message.ComputeHash();
            return envelope.IsSignatureValid(keyPair, messageHash, allowSignless);
        }

        /// <summary>
        /// Checks that envelope signature is valid
        /// </summary>
        /// <param name="envelope">Target envelope</param>
        /// <returns>True if signature is valid, otherwise false</returns>
        public static bool IsSignatureValid(this MessageEnvelopeBase envelope, KeyPair keyPair, byte[] messageHash, bool allowSignless = true)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            if (messageHash == null)
                throw new ArgumentNullException(nameof(messageHash));

            if (keyPair == null)
                throw new ArgumentNullException(nameof(keyPair));

            if (envelope is MessageEnvelopeSignless)
                return allowSignless;

            if (envelope is MessageEnvelope messageEnvelope && messageEnvelope.Signature != null)
            {
                return messageEnvelope.Signature.IsValid(keyPair, messageHash);
            }
            else if (envelope is ConstellationMessageEnvelope constellationMessageEnvelope
                && constellationMessageEnvelope.Signatures.Any(s => s.IsValid(keyPair, messageHash)))
                return true;
            return false;
        }

        private static TResultMessage CreateResult<TResultMessage>(this MessageEnvelopeBase envelope, ResultStatusCode status = ResultStatusCode.InternalError)
            where TResultMessage : ResultMessageBase
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            var resultMessage = Activator.CreateInstance<TResultMessage>();
            var messageId = envelope.Message.MessageId;
            if (envelope.Message is ClientRequestQuantumBase request)
                messageId = request.RequestMessage.MessageId;
            resultMessage.OriginalMessageId = messageId;
            resultMessage.Status = status;
            return resultMessage;
        }

        public static ResultMessageBase CreateResult(this MessageEnvelopeBase envelope, ResultStatusCode status = ResultStatusCode.InternalError)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            var messageType = envelope.Message;
            if (envelope.Message is ClientRequestQuantumBase)
                messageType = ((ClientRequestQuantumBase)envelope.Message).RequestEnvelope.Message;

            //for not Success result return generic message
            if (status == ResultStatusCode.Success)
                switch (messageType)
                {
                    case AccountDataRequest _:
                        return CreateResult<AccountDataResponse>(envelope, status);
                    default:
                        break;
                }

            if (envelope.Message is Quantum)
            {
                var quantumResult = CreateResult<QuantumResultMessage>(envelope, status);
                //TODO: remove it after migrating to another serializer
                if (status != ResultStatusCode.Success)
                {
                    quantumResult.Effects = new List<EffectsInfoBase>();
                    quantumResult.PayloadProof = new PayloadProof
                    {
                        PayloadHash = new byte[] { },
                        Signatures = new List<TinySignature>()
                    };
                }
                return quantumResult;
            }
            return CreateResult<ResultMessage>(envelope, status);
        }

        public static ResultMessageBase CreateResult(this MessageEnvelopeBase envelope, Exception exc)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            if (exc == null)
                throw new ArgumentNullException(nameof(exc));
            var result = envelope.CreateResult(exc.GetStatusCode());
            if (!(result.Status == ResultStatusCode.InternalError || string.IsNullOrWhiteSpace(exc.Message)))
                result.ErrorMessage = exc.Message;
            else
                result.ErrorMessage = result.Status.ToString();
            return result;
        }
    }
}
