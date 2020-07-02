using Centaurus.DAL.Models;
using Centaurus.Models;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class QuantumModelExtensions
    {
        public static QuantumModel FromQuantum(MessageEnvelope quantum)
        {
            if (quantum == null)
                throw new ArgumentNullException(nameof(quantum));
            byte[] account = null;
            var quantumMessage = (Quantum)quantum.Message;
            if (quantumMessage is RequestQuantum)
            {
                var request = ((RequestQuantum)quantumMessage).RequestEnvelope;
                if (request.Signatures.Count < 1)
                    throw new Exception("A quantum lack signatures.");
                account = request.Signatures.First().Signer.Data;
            }
            return new QuantumModel
            {
                Apex = quantumMessage.Apex,
                Account = account,
                RawQuantum = XdrConverter.Serialize(quantum),
                Type = (int)quantumMessage.MessageType,
                TimeStamp = DateTime.UtcNow
            };
        }

        public static MessageEnvelope ToMessageEnvelope(this QuantumModel quantum)
        {
            if (quantum == null)
                throw new ArgumentNullException(nameof(quantum));
            return XdrConverter.Deserialize<MessageEnvelope>(quantum.RawQuantum);
        }
    }
}
