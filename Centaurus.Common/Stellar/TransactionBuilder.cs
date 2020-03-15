using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class TransactionBuilderOptions
    {
        public TransactionBuilderOptions(AccountData source, uint fee, string memo = null)
        {
            if (fee <= 0)
                throw new ArgumentNullException(nameof(fee));

            Source = source ?? throw new ArgumentNullException(nameof(source));
            Fee = fee;
            Memo = memo;
        }

        public AccountData Source { get; set; }

        public uint Fee { get; set; }
        
        public string Memo { get; set; }
    }

    public static class TransactionHelper
    {
        public static Transaction BuildPaymentTransaction(TransactionBuilderOptions options, KeyPair destination, Asset asset, string amount)
        {
            var paymentOperation = new PaymentOperation.Builder(destination, asset, amount).Build();

            return BuildTransaction(options, paymentOperation);
        }

        public static Transaction BuildBumpSequenceTransaction(TransactionBuilderOptions options, long bumpTo)
        {
            var bumpToOp = new BumpSequenceOperation.Builder(bumpTo).Build();

            return BuildTransaction(options, bumpToOp);
        }

        public static Transaction BuildTransaction(TransactionBuilderOptions options, params Operation[] operations)
        {
            var builder = new Transaction.Builder(options.Source);
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
    }
}
