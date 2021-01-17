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
            var quantumMessage = (Quantum)quantum.Message;
            var account = 0;
            if (quantumMessage is RequestQuantum requestQuantum)
                account = requestQuantum.RequestMessage.Account;

            return new QuantumModel
            {
                Apex = quantumMessage.Apex,
                Account = account,
                RawQuantum = XdrConverter.Serialize(quantum),
                Type = (int)quantumMessage.MessageType,
                TimeStamp = quantumMessage.Timestamp
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
