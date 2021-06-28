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
    public abstract class PaymentProviderBase : IDisposable
    {
        public PaymentProviderBase(PaymentParserBase parser, ProviderSettings settings, dynamic config, WithdrawalStorage withdrawalStorage)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));


            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Parser = parser ?? throw new ArgumentNullException(nameof(parser));
            Id = GetProviderId(settings.Provider, settings.Name);
            Secret = config.Secret ?? throw new ArgumentNullException(nameof(config.Secret));
            MaxTxSubmitDelay = ((long?)config.MaxTxSubmitDelay) ?? throw new ArgumentNullException(config.MaxTxSubmitDelay);
            WithdrawalStorage = withdrawalStorage ?? throw new ArgumentNullException(nameof(withdrawalStorage));
            NotificationsManager = new PaymentNotificationManager(settings.Cursor, Parser);
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

        public string Id { get; }

        public string Secret { get; }

        public long MaxTxSubmitDelay { get; }

        public PaymentParserBase Parser { get; }

        public WithdrawalStorage WithdrawalStorage { get; }

        public PaymentNotificationManager NotificationsManager { get; }

        public string Cursor => NotificationsManager?.Cursor;

        public string LastRegisteredCursor => NotificationsManager?.LastRegisteredCursor;

        public abstract TxSignature SignTransaction(TransactionWrapper transaction);

        public abstract void ValidateTransaction(TransactionWrapper transaction);

        public abstract WithdrawalWrapper GetWithdrawal(MessageEnvelope envelope, AccountWrapper account, TransactionWrapper transactionWrapper);

        public abstract void Dispose();

        public static string GetProviderId(string provider, string name)
        {
            if (string.IsNullOrWhiteSpace(provider))
                throw new ArgumentNullException(nameof(provider));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            return $"{provider}-{name}";
        }
    }
}