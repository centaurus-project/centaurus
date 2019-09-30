using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class TransactionExtensions
    {
        public static byte[] ToRawEnvelopeXdr(this Transaction tx)
        {
            //TODO: performance: serialize directly, without Base64 encoding
            return Convert.FromBase64String(tx.ToEnvelopeXdrBase64());
        }
    }
}
