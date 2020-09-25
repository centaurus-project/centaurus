using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.SDK
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
