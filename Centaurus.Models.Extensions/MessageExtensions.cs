using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public static class MessageExtensions
    {
        public static MessageEnvelope CreateEnvelope(this Message message, List<SideEffect> sideEffects = null)
        {
            var envelope = new MessageEnvelope
            {
                Message = message,
                Signatures = new List<Ed25519Signature>()
            };
            if (sideEffects != null && sideEffects.Count > 0)
                envelope.SideEffects = sideEffects;
            return envelope;
        }
    }
}
