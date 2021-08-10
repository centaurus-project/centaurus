using System;
using Centaurus.Models;

namespace Centaurus.NetSDK
{
    public class RequestException: Exception
    {
        public RequestException(MessageEnvelope envelope, string message = null)
            :base(message)
        {
            Envelope = envelope;
        }

        public MessageEnvelope Envelope { get; }
    }
}
