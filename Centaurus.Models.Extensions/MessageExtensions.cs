using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public static class MessageExtensions
    {
        public static MessageEnvelope CreateEnvelope(this Message message)
        {
            return message.CreateEnvelope<MessageEnvelope>();
        }

        public static T CreateEnvelope<T>(this Message message)
            where T : MessageEnvelopeBase
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var envelope = Activator.CreateInstance<T>();
            envelope.Message = message;
            if (envelope is ConstellationMessageEnvelope messageEnvelope)
                messageEnvelope.Signatures = new List<TinySignature>();
            return envelope;
        }

        public static string GetMessageType(this Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return message.GetType().Name;
        }
    }
}