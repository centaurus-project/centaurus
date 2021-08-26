using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class SignatureModelExtensions
    {
        public static SignatureModel ToPersistenModel(this AuditorSignatureInternal auditorSignature)
        {
            if (auditorSignature == null)
                throw new ArgumentNullException(nameof(auditorSignature));

            return new SignatureModel
            {
                AuditorId = (byte)auditorSignature.AuditorId,
                PayloadSignature = auditorSignature.PayloadSignature.Data,
                TxSignature = auditorSignature.TxSignature,
                TxSigner = auditorSignature.TxSigner
            };
        }

        public static AuditorSignatureInternal ToDomainModel(this SignatureModel auditorSignature)
        {
            if (auditorSignature == null)
                throw new ArgumentNullException(nameof(auditorSignature));

            return new AuditorSignatureInternal
            {
                AuditorId = auditorSignature.AuditorId,
                PayloadSignature = new TinySignature { Data = auditorSignature.PayloadSignature },
                TxSignature = auditorSignature.TxSignature,
                TxSigner = auditorSignature.TxSigner
            };
        }
    }
}
