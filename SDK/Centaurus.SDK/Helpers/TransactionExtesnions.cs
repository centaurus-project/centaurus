using Centaurus.SDK.Models;
using Centaurus.Stellar;
using Centaurus.Stellar.Models;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Centaurus.SDK
{
    public static class TransactionExtesnions
    {
        public static byte[] ToArray(this stellar_dotnet_sdk.Transaction tx)
        {
            var outputStream = new XdrDataOutputStream();
            stellar_dotnet_sdk.xdr.Transaction.Encode(outputStream, tx.ToXdrV1());
            return outputStream.ToArray();
        }
    }
}
