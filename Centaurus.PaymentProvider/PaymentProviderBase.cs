using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;

namespace Centaurus.PaymentProvider
{
    /// <summary>
    /// Every child class should follow next naming convention "<Name>PaymentProvider"
    /// </summary>
    public abstract class PaymentProviderBase : ICursorComparer, IDisposable
    {
        public PaymentProviderBase(SettingsModel settings, string rawConfig)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            NotificationsManager = new DepositNotificationManager(settings.InitCursor, this);
        }

        public event Action<PaymentProviderBase, DepositNotificationModel> OnPaymentCommit;

        protected void RaiseOnPaymentCommit(DepositNotificationModel deposit)
        {
            OnPaymentCommit?.Invoke(this, deposit);
        }

        public event Action<Exception> OnError;

        protected void RaiseOnError(Exception exc)
        {
            OnError?.Invoke(exc);
        }

        public SettingsModel Settings { get; }

        public string Provider => Settings.Provider;

        public string Vault => Settings.Vault;

        public string Id => Settings.Id;

        public DepositNotificationManager NotificationsManager { get; }

        public string Cursor => NotificationsManager?.Cursor;

        public string LastRegisteredCursor => NotificationsManager?.LastRegisteredCursor;

        public abstract byte[] BuildTransaction(WithdrawalRequestModel withdrawalRequest);

        /// <summary>
        /// Should throw error if transaction is not valid
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="withdrawalRequest"></param>
        public abstract void ValidateTransaction(byte[] transaction, WithdrawalRequestModel withdrawalRequest);

        public abstract SignatureModel SignTransaction(byte[] transaction);

        public abstract void SubmitTransaction(byte[] transaction, List<SignatureModel> signatures);
        
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns>
        /// A signed integer that indicates the relative values of x and y, as shown in the
        /// following table. Value Meaning Less than zero x is less than y. Zero x equals
        /// y. Greater than zero x is greater than y.
        /// </returns>
        public abstract int CompareCursors(string left, string right);

        public abstract void Dispose();

        public static string GetProviderId(string provider, string name)
        {
            return $"{provider}-{name}";
        }
    }
}