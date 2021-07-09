using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class SignaturePersistentModelExtenstions
    {
        public static SignaturePersistentModel ToPersistentModel(this Ed25519Signature signature)
        {
            if (signature == null)
                throw new ArgumentNullException(nameof(signature));

            return new SignaturePersistentModel
            {
                Signer = signature.Signer,
                Data = signature.Signature
            };
        }

        public static Ed25519Signature ToDomainModel(this SignaturePersistentModel signature)
        {
            if (signature == null)
                throw new ArgumentNullException(nameof(signature));

            return new Ed25519Signature
            {
                Signer = signature.Signer,
                Signature = signature.Data
            };
        }
    }
}
