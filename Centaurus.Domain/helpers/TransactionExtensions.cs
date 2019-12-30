using stellar_dotnet_sdk;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class TransactionExtensions
    {
        public static byte[] ToRawEnvelopeXdr(this stellar_dotnet_sdk.Transaction tx)
        {
            var outputStream = new XdrDataOutputStream();
            TransactionEnvelope.Encode(outputStream, tx.ToUnsignedEnvelopeXdr());
            return outputStream.ToArray();
        }
    }
}
