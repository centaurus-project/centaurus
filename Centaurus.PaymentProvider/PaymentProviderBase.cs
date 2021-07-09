using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.PaymentProvider
{
    /// <summary>
    /// Every child class should follow next naming convention "<Name>PaymentProvider"
    /// </summary>
    public abstract class PaymentProviderBase : ICursorComparer, IDisposable
    {
        public PaymentProviderBase(ProviderSettings settings, string rawConfig)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            NotificationsManager = new DepositNotificationManager(settings.InitCursor, this);
        }

        public event Action<MessageEnvelope> OnPaymentCommit;

        protected void RaiseOnPaymentCommit(MessageEnvelope envelope)
        {
            OnPaymentCommit?.Invoke(envelope);
        }

        public event Action<MessageEnvelope> OnCleanup;

        protected void RaiseOnCleanup(MessageEnvelope envelope)
        {
            OnCleanup?.Invoke(envelope);
        }

        public event Action<Exception> OnError;

        protected void RaiseOnError(Exception exc)
        {
            OnError?.Invoke(exc);
        }

        public ProviderSettings Settings { get; }

        public string Provider => Settings.Provider;

        public string Vault => Settings.Vault;

        public string Id => Settings.ProviderId;

        public DepositNotificationManager NotificationsManager { get; }

        public string Cursor => NotificationsManager?.Cursor;

        public string LastRegisteredCursor => NotificationsManager?.LastRegisteredCursor;

        public abstract byte[] BuildTransaction(WithdrawalRequest withdrawalRequest);

        /// <summary>
        /// Should throw error if transaction is not valid
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="withdrawalRequest"></param>
        public abstract void ValidateTransaction(byte[] transaction, WithdrawalRequest withdrawalRequest);

        public abstract TxSignature SignTransaction(byte[] transaction);

        public abstract void SubmitTransaction(byte[] transaction, List<TxSignature> signatures);

        public abstract int CompareCursors(string left, string right);

        public abstract void Dispose();
    }
}