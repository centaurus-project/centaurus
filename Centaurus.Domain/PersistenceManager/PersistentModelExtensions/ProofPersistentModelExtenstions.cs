using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class ProofPersistentModelExtenstions
    {
        public static QuantumProofPersistentModel ToPersistentModel(this EffectsProof proof)
        {
            if (proof == null)
                throw new ArgumentNullException(nameof(proof));

            return new QuantumProofPersistentModel
            {
                EffectHashes = proof.Hashes.Hashes.Select(h => h.Data).ToList(),
                Signatures = proof.Signatures.Select(s => s.ToPersistentModel()).ToList()
            };
        }

        public static EffectsProof ToDomainModel(this QuantumProofPersistentModel proof)
        {
            if (proof == null)
                throw new ArgumentNullException(nameof(proof));

            return new EffectsProof
            {
                Hashes = new EffectHashes { Hashes = proof.EffectHashes.Select(h => new Hash { Data = h }).ToList() },
                Signatures = proof.Signatures.Select(s => s.ToDomainModel()).ToList()
            };
        }
    }
}
