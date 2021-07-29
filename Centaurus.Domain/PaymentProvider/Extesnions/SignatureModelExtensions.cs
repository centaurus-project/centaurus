using Centaurus.Models;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class SignatureModelExtensions
    {
        public static SignatureModel ToProviderModel(this TxSignature signature)
        {
            if (signature == null)
                throw new ArgumentNullException(nameof(signature));

            return new SignatureModel { Signer = signature.Signer, Signature = signature.Signature };
        }

        public static TxSignature ToDomainModel(this SignatureModel signature)
        {
            if (signature == null)
                throw new ArgumentNullException(nameof(signature));

            return new TxSignature { Signer = signature.Signer, Signature = signature.Signature };
        }
    }
}
