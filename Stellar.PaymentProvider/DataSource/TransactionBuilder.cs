using stellar_dotnet_sdk;
using stellar_dotnet_sdk.requests;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus
{
    public class TransactionBuilderOptions
    {
        public TransactionBuilderOptions(Account source, uint fee, string memo = null)
        {
            if (fee <= 0)
                throw new ArgumentNullException(nameof(fee));

            Source = source ?? throw new ArgumentNullException(nameof(source));
            Fee = fee;
            Memo = memo;
        }

        public Account Source { get; set; }

        public uint Fee { get; set; }
        
        public string Memo { get; set; }
    }

    public static class TransactionHelper
    {
        public static Transaction BuildPaymentTransaction(TransactionBuilderOptions options, KeyPair destination, Asset asset, long amount)
        {
            var paymentOperation = new PaymentOperation.Builder(destination, asset, Amount.FromXdr(amount)).Build();

            return BuildTransaction(options, paymentOperation);
        }

        public static Transaction BuildBumpSequenceTransaction(TransactionBuilderOptions options, long bumpTo)
        {
            var bumpToOp = new BumpSequenceOperation.Builder(bumpTo).Build();

            return BuildTransaction(options, bumpToOp);
        }

        public static Transaction BuildTransaction(TransactionBuilderOptions options, params Operation[] operations)
        {
            var builder = new TransactionBuilder(options.Source);
            builder.SetFee(options.Fee);
            if (!string.IsNullOrWhiteSpace(options.Memo))
                builder.AddMemo(Memo.Text(options.Memo));

            foreach (var op in operations)
            {
                builder.AddOperation(op);
            }

            var transaction = builder.Build();

            return transaction;
        }

        /// <summary>
        /// Returns tx by hash or null if not found
        /// </summary>
        /// <param name="transactionHash"></param>
        /// <returns></returns>
        public static async Task<TransactionResponse> GetTransaction(this Server server, string transactionHash)
        {
            if (server == null)
                throw new ArgumentNullException(nameof(server));
            if (transactionHash == null || transactionHash.Length == 0)
                throw new ArgumentNullException(nameof(transactionHash));
            try
            {
                return await server.Transactions.IncludeFailed(true).Transaction(transactionHash);
            }
            catch (HttpResponseException exc)
            {
                if (exc.StatusCode != 404)
                    throw;
                return null;
            }
        }

        public static TransactionsRequestBuilder GetTransactionsRequestBuilder(this Server server, string pubKey, long cursor, int limit = 200, bool includeFailed = true)
        {
            return server.Transactions
                .ForAccount(pubKey)
                .IncludeFailed(includeFailed)
                .Limit(limit)
                .Cursor(cursor.ToString());
        }
    }
}
