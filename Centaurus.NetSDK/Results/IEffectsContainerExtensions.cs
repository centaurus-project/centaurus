using Centaurus.Models;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.NetSDK
{
    public static class IEffectsContainerExtensions
    {
        public static int GetValidSignaturesCount(this IQuantumInfoContainer quantumInfo, List<KeyPair> validSigners)
        {
            if (quantumInfo == null)
                throw new ArgumentNullException(nameof(quantumInfo));

            if (validSigners == null)
                throw new ArgumentNullException(nameof(validSigners));
            if (validSigners.Count == 0)
                throw new ArgumentException("Lack of valid signers.", nameof(validSigners));

            //check that all signatures are unique
            if (quantumInfo.PayloadProof.Signatures.Distinct().Count() != quantumInfo.PayloadProof.Signatures.Count)
                return 0;

            //count valid signatures
            var validSignatures = 0;
            foreach (var singnature in quantumInfo.PayloadProof.Signatures)
            {
                if (validSigners.Any(s => singnature.IsValid(s, quantumInfo.PayloadProof.PayloadHash)))
                    validSignatures++;
            }

            var effectsBytes = (IEnumerable<byte>)new byte[] { };
            //validate payload hash is valid
            foreach (var effectsInfo in quantumInfo.Effects)
            {
                if (effectsInfo is EffectsHashInfo)
                    effectsBytes = effectsBytes.Concat(effectsInfo.EffectsGroupData);
                else
                    effectsBytes = effectsBytes.Concat(effectsInfo.EffectsGroupData.ComputeHash());
            }

            //compute payload hash
            var payloadHash = ByteArrayExtensions.ComputeQuantumPayloadHash(quantumInfo.Apex, quantumInfo.QuantumHash, effectsBytes.ToArray().ComputeHash());

            if (!ByteArrayPrimitives.Equals(quantumInfo.PayloadProof.PayloadHash, payloadHash))
                return 0;

            return validSignatures;
        }
    }
}
