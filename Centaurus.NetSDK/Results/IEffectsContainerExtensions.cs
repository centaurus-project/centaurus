using Centaurus.Models;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.NetSDK
{
    public static class IEffectsContainerExtensions
    {
        public static int GetValidSignaturesCount(this IEffectsContainer effects, List<KeyPair> validSigners)
        {
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));

            if (validSigners == null)
                throw new ArgumentNullException(nameof(validSigners));
            if (validSigners.Count == 0)
                throw new ArgumentException("Lack of valid signers.", nameof(validSigners));

            //check that all signatures are unique
            if (effects.Effects.Signatures.Distinct().Count() != effects.Effects.Signatures.Count)
                return 0;

            //compute hashes hash
            var hash = effects.Effects.Hashes.SelectMany(h => h.Data).ComputeHash();

            //count valid signatures
            var validSignatures = 0;
            foreach (var singnature in effects.Effects.Signatures)
            {
                if (validSigners.Any(s => singnature.IsValid(s, hash)))
                    validSignatures++;
            }

            //validate that client effect hashes equal to signed hashes
            for (var i = 0; i < effects.ClientEffects.Count; i++)
            {
                var currentEffect = effects.ClientEffects[i];
                if (currentEffect == null)
                    continue;

                var currentEffectHash = effects.Effects.Hashes[i];

                if (!XdrConverter.Serialize(currentEffect).ComputeHash().SequenceEqual(currentEffectHash.Data))
                    return 0;
            }
            return validSignatures;
        }
    }
}
