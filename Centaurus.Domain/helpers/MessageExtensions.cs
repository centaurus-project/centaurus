using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class MessageExtensions
    {
        public static MessageEnvelope CreateEnvelope(this Message message)
        {
            return new MessageEnvelope
            {
                Message = message,
                Signatures = new List<Ed25519Signature>()
            };
        }
    }
}
