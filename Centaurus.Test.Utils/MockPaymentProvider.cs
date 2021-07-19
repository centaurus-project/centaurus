using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using System.Collections.Generic;

namespace Centaurus.Test
{
    internal class MockPaymentProvider : PaymentProviderBase
    {
        public MockPaymentProvider(SettingsModel settings, string config) 
            : base(settings, config)
        {
        }

        public override byte[] BuildTransaction(WithdrawalRequestModel withdrawalRequest)
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

        public override SignatureModel SignTransaction(byte[] transaction)
        {
            return new SignatureModel { Signer = new byte[] { }, Signature = new byte[] { } };
        }

        public override void SubmitTransaction(byte[] transaction, List<SignatureModel> signatures)
        {
            return;
        }

        public override void ValidateTransaction(byte[] transaction, WithdrawalRequestModel withdrawalRequest)
        {
            return;
        }
    }
}