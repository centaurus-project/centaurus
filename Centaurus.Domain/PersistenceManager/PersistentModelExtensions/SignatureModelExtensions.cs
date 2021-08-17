using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class SignatureModelExtensions
    {
        public static SignatureModel ToPersistenModel(this AuditorSignature auditorSignature)
        {
            if (auditorSignature == null)
                throw new ArgumentNullException(nameof(auditorSignature));

            return new SignatureModel
            {
                PayloadSignature = auditorSignature.PayloadSignature.Data,
                TxSignature = auditorSignature.TxSignature,
                TxSigner = auditorSignature.TxSigner
            };
        }
    }
}
