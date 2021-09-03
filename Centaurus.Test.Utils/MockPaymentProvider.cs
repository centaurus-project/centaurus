using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Centaurus.Test
{
    public class MockPaymentProvider : PaymentProviderBase
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

        public override bool IsTransactionValid(byte[] transaction, WithdrawalRequestModel withdrawalRequestm, out string error)
        {
            error = null;
            return true;
        }

        public void Deposit(DepositNotificationModel depositNotificationModel)
        {
            NotificationsManager.RegisterNotification(depositNotificationModel);
            try
            {
                var commitPaymentsMethod = typeof(PaymentProviderBase).GetMethod("CommitPayments", BindingFlags.Instance | BindingFlags.NonPublic);
                commitPaymentsMethod.Invoke(this, null);
            }
            catch (OperationCanceledException)
            { }
        }

        public override bool AreSignaturesValid(byte[] transaction, params SignatureModel[] signature)
        {
            return true;
        }
    }
}