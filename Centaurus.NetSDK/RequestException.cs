using System;
using Centaurus.Models;

namespace Centaurus.NetSDK
{
    public class RequestException: Exception
    {
        public RequestException(MessageEnvelopeBase envelope, string message = null)
            :base(message)
        {
            Envelope = envelope;
        }

        public MessageEnvelopeBase Envelope { get; }
    }
}
