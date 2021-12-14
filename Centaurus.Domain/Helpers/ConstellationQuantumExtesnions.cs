using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class ConstellationQuantumExtesnions
    {
        public static void Validate(this ConstellationQuantum constellationQuantum, ExecutionContext context)
        {
            if (constellationQuantum == null)
                throw new ArgumentNullException(nameof(constellationQuantum));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (constellationQuantum.RequestMessage == null || !(constellationQuantum.RequestMessage is ConstellationRequestMessage))
                throw new Exception("Invalid message type.");

            var envelope = constellationQuantum.RequestEnvelope as ConstellationMessageEnvelope;
            if (envelope == null)
                throw new InvalidOperationException($"Constellation quantum must be wrapped with {typeof(ConstellationMessageEnvelope).Name}.");
            var signatures = envelope.Signatures;

            //validate that signatures are unique
            if (signatures.Select(s => s.Data).Distinct(ByteArrayComparer.Default).Count() != signatures.Count)
                throw new Exception("All signatures must be unique.");

            //get auditors
            var auditors = context.GetRelevantAuditors();

            if (MajorityHelper.GetMajorityCount(auditors.Count()) > signatures.Count)
                throw new Exception("Envelope has no majority.");

            var messageHash = constellationQuantum.RequestMessage.ComputeHash();

            var invalidatedAuditors = auditors.ToList();
            foreach (var signature in signatures)
            {
                foreach (var auditor in invalidatedAuditors)
                {
                    if (signature.IsValid(auditor, messageHash))
                    {
                        invalidatedAuditors.Remove(auditor);
                        break;
                    }
                }
            }
            if (invalidatedAuditors.Count > 0)
                throw new Exception("Invalid signature.");
        }
    }
}
