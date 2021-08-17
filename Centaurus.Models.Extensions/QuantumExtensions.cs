using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public static class QuantumExtensions
    {
        public static byte[] GetPayloadHash(this Quantum quantum)
        {
            if (quantum == null)
                throw new ArgumentNullException(nameof(quantum));
            return ByteArrayExtensions.ComputeQuantumPayloadHash(quantum.Apex, quantum.ComputeHash(), quantum.EffectsProof);
        }
    }
}
