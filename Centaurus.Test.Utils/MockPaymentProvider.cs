using Centaurus.Models;
using Centaurus.PaymentProvider;
using System.Collections.Generic;

namespace Centaurus.Test
{
    internal class MockPaymentProvider : PaymentProviderBase
    {
        public MockPaymentProvider(ProviderSettings settings, string config) 
            : base(settings, config)
        {
        }

        public override byte[] BuildTransaction(WithdrawalRequest withdrawalRequest)
        {
            return new byte[] { };
        }

        public override int CompareCursors(string left, string right)
        {
            return Comparer<int>.Default.Compare(int.Parse(left), int.Parse(right));
        }

        public override void Dispose()
        {
            return;
        }

        public override TxSignature SignTransaction(byte[] transaction)
        {
            return new TxSignature { Signer = new byte[] { }, Signature = new byte[] { } };
        }

        public override void SubmitTransaction(byte[] transaction, List<TxSignature> signatures)
        {
            return;
        }

        public override void ValidateTransaction(byte[] transaction, WithdrawalRequest withdrawalRequest)
        {
            return;
        }
    }
}