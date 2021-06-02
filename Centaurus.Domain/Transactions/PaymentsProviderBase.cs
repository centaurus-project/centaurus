using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public abstract class PaymentsProviderBase : ContextualBase, IDisposable
    {
        public PaymentsProviderBase(ExecutionContext executionContext)
            :base(executionContext)
        {
        }

        public abstract PaymentProvider Provider { get; }

        public WithdrawalStorage WithdrawalStorage { get; protected set; }
        public PaymentsParserBase PaymentsParser { get; private set; }
        public PaymentNotificationManager NotificationsManager { get; protected set; }

        public string Cursor => NotificationsManager?.Cursor;

        public string LastRegisteredCursor => NotificationsManager?.LastRegisteredCursor;


        public virtual void Init(string vault, string currentCursor, string secret, PaymentsParserBase paymentsParser, WithdrawalStorage withdrawalStorage)
        {
            if (WithdrawalStorage != null)
                throw new InvalidOperationException("Already initialized.");
            WithdrawalStorage = withdrawalStorage ?? throw new ArgumentNullException(nameof(withdrawalStorage));
            PaymentsParser = paymentsParser ?? throw new ArgumentNullException(nameof(paymentsParser));
            NotificationsManager = new PaymentNotificationManager(currentCursor, PaymentsParser);
            Secret = secret ?? throw new ArgumentNullException(nameof(secret));
            Vault = vault ?? throw new ArgumentNullException(nameof(vault));
        }

        public abstract void Dispose();

        public string Secret { get; protected set; }

        public string Vault { get; protected set; }

        //TODO: create base class for tx signatures. 
        public abstract Ed25519Signature SignTransaction(TransactionWrapper transaction);
    }

    public abstract class PaymentsProviderBase<TVault, TSecret, TCursor> : PaymentsProviderBase
    {
        public PaymentsProviderBase(ExecutionContext executionContext)
            : base(executionContext)
        {
        }

        public override void Init(string vault, string cursor, string secret, PaymentsParserBase paymentsParser, WithdrawalStorage withdrawalStorage)
        {
            base.Init(vault, cursor, secret, paymentsParser, withdrawalStorage);
            Secret = (TSecret)paymentsParser.ParseSecret(secret);
            Vault = (TVault)paymentsParser.ParseVault(vault);
        }

        public new TSecret Secret { get; protected set; }

        public new TVault Vault { get; protected set; }

        public new TCursor Cursor => (TCursor)PaymentsParser.ParseCursor(base.Cursor);

        public new TCursor LastRegisteredCursor => (TCursor)PaymentsParser.ParseCursor(base.LastRegisteredCursor);
    }
}