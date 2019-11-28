using Centaurus.DAL.Models;
using Centaurus.Models;
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
            byte[] account = null;
            var quantumMessage = quantum.Message as Quantum;
            if (quantumMessage is RequestQuantum)
                account = ((RequestQuantum)quantumMessage).RequestEnvelope.Signatures.First().Signer.Data;
            return new QuantumModel
            {
                Apex = quantumMessage.Apex,
                Account = account,
                RawQuantum = XdrConverter.Serialize(quantum),
                Type = (int)quantumMessage.MessageType,
                TimeStamp = DateTime.UtcNow
            };
        }
    }
}
