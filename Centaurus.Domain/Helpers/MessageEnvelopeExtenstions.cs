using Centaurus.Models;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class MessageEnvelopeExtenstions
    {
        public static IncomingMessage ToIncomingMessage(this MessageEnvelope envelope, XdrBufferFactory.RentedBuffer rentedBuffer)
        {
            using var writer = new XdrBufferWriter(rentedBuffer.Buffer);
            return envelope.ToIncomingMessage(writer);
        }

        public static IncomingMessage ToIncomingMessage(this MessageEnvelope envelope, XdrBufferWriter writer)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));
            XdrConverter.Serialize(envelope.Message, writer);
            var messageHash = writer.ToArray().ComputeHash();
            return new IncomingMessage(envelope, messageHash);
        }
    }
}
