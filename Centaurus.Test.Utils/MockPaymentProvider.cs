using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class MockPaymentProvider : PaymentProviderBase
    {
        public MockPaymentProvider(SettingsModel settings, string config) 
            : base(settings, config)
        {
            ReloadDeposits(this);
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

        public override Task<bool> SubmitTransaction(byte[] transaction, List<SignatureModel> signatures)
        {
            return Task.FromResult(true);
        }

        public override bool IsTransactionValid(byte[] transaction, WithdrawalRequestModel withdrawalRequestm, out string error)
        {
            error = null;
            return true;
        }

        private static List<DepositNotificationModel> allDeposits = new List<DepositNotificationModel>();

        public void AddDeposit(DepositNotificationModel depositNotificationModel)
        {
            allDeposits.Add(depositNotificationModel);
            RegisterDeposit(depositNotificationModel);
        }

        private void RegisterDeposit(DepositNotificationModel depositNotificationModel)
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

        private static void ReloadDeposits(MockPaymentProvider provider)
        {
            foreach (var deposit in allDeposits.Where(d => provider.CompareCursors(d.Cursor, provider.Cursor) > 0))
                provider.RegisterDeposit(deposit);
        }

        public override bool AreSignaturesValid(byte[] transaction, params SignatureModel[] signature)
        {
            return true;
        }
    }
}