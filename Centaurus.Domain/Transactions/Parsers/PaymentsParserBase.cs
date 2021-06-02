using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class PaymentsParserBase
    {
        public abstract PaymentProvider Provider { get; }

        public abstract bool TryDeserializeTransaction(byte[] rawTransaction, out TransactionWrapper transaction);

        public abstract void ValidateTransaction(TransactionWrapper transaction, string vault);
        
        public abstract WithdrawalWrapper GetWithdrawal(MessageEnvelope envelope, TransactionWrapper transactionWrapper, ConstellationSettings constellationSettings, string vault);

        public abstract int CompareCursors(string left, string right);

        public abstract object ParseSecret(string secret);

        public abstract object ParseVault(string vault);

        public abstract object ParseCursor(string cursor);
    }

    public abstract class PaymentsParserBase<TTranscation>: PaymentsParserBase
    {
        public abstract bool TryDeserializeTransaction(byte[] rawTransaction, out TransactionWrapper<TTranscation> transaction);

        public override bool TryDeserializeTransaction(byte[] rawTransaction, out TransactionWrapper transaction)
        {
            var result = TryDeserializeTransaction(rawTransaction, out var tx);
            transaction = tx;
            return result;
        }

        public abstract void ValidateTransaction(TransactionWrapper<TTranscation> transaction, string vault);

        public override void ValidateTransaction(TransactionWrapper transaction, string vault)
        {
            ValidateTransaction((TransactionWrapper<TTranscation>)transaction, vault);
        }

        public abstract WithdrawalWrapper GetWithdrawal(MessageEnvelope envelope, TransactionWrapper<TTranscation> transactionWrapper, ConstellationSettings constellationSettings, string vault);

        public override WithdrawalWrapper GetWithdrawal(MessageEnvelope envelope, TransactionWrapper transactionWrapper, ConstellationSettings constellationSettings, string vault)
        {
            return GetWithdrawal(envelope, (TransactionWrapper<TTranscation>)transactionWrapper, constellationSettings, vault);
        }
    }
}
