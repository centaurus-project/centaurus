using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Models
{
    public static class EffectsProofExtensions
    {
        public static bool ContainsSingnature(this EffectsProof effectsProof, byte[] signature)
        {
            if (effectsProof == null)
                throw new ArgumentNullException(nameof(effectsProof));

            if (signature == null)
                throw new ArgumentNullException(nameof(signature));

            return effectsProof.Signatures != null && effectsProof.Signatures.Any(s => s.Data.SequenceEqual(signature));
        }
    }
}
