using Centaurus.SDK.Models;
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

        public static async Task<SubmitTransactionResponse> Submit(this stellar_dotnet_sdk.Transaction tx, ConstellationInfo constellation)
        {
            using (var server = constellation.StellarNetwork.GetServer())
            {
                var res = await server.SubmitTransaction(tx);
                if (!res.IsSuccess())
                {
                    throw new Exception($"Tx submit error. Result xdr: {res.ResultXdr}");
                }
                return res;
            }
        }
    }
}
