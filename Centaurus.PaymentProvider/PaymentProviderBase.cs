using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;

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
            NotificationsManager = new DepositNotificationManager(settings.Cursor, this);

            commitDelay = TimeSpan.FromSeconds(settings.PaymentSubmitDelay);
            submitTimerInterval = TimeSpan.FromSeconds(5).TotalMilliseconds;
            InitTimer();
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
        public abstract bool IsTransactionValid(byte[] transaction, WithdrawalRequestModel withdrawalRequest, out string error);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="signature"></param>
        /// <returns></returns>
        public abstract bool AreSignaturesValid(byte[] transaction, params SignatureModel[] signature);

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


        public virtual void Dispose()
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();

                lock (submitTimer)
                {
                    submitTimer.Stop();
                    submitTimer.Dispose();
                }
            }
        }


        protected readonly TimeSpan commitDelay;

        protected CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        readonly System.Timers.Timer submitTimer = new System.Timers.Timer();

        readonly double submitTimerInterval;

        void StartTimer()
        {

            lock (submitTimer)
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                    submitTimer.Start();
            }
        }

        void CommitPayments()
        {
            foreach (var payment in NotificationsManager.GetAll())
            {
                if (DateTime.UtcNow - payment.DepositTime < commitDelay)
                    break;
                if (payment.IsSend)
                    continue;

                RaiseOnPaymentCommit(payment);
                //mark as send
                payment.IsSend = true;
            }
        }


        void InitTimer()
        {
            lock (submitTimer)
            {
                submitTimer.Interval = submitTimerInterval;
                submitTimer.AutoReset = false;
                submitTimer.Elapsed += SubmitTimer_Elapsed;
                StartTimer();
            }
        }

        void SubmitTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                CommitPayments();
            }
            catch (Exception exc)
            {
                //TODO: log
            }
            StartTimer();
        }

        public static string GetProviderId(string provider, string name)
        {
            return $"{provider}-{name}";
        }
    }
}