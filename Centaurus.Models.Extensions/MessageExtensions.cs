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
            var envelope = new MessageEnvelope
            {
                Message = message,
                Signatures = new List<Ed25519Signature>()
            };
            return envelope;
        }
    }
}
