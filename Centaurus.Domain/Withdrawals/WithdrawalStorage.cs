using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class WithdrawalStorage : IDisposable
    {
        public WithdrawalStorage(IEnumerable<Withdrawal> payments, bool startSubmitTimer = true)
        {
            withdrawals = new Dictionary<byte[], Withdrawal>(new HashComparer());

            if (payments == null)
                return;
            foreach (var payment in payments.OrderBy(p => p.Apex))
                Add(payment);
            if (startSubmitTimer)
                InitTimer();
        }

        public void Add(Withdrawal payment)
        {
            if (payment == null)
                throw new ArgumentNullException(nameof(payment));

            lock (withdrawals)
                if (!withdrawals.TryAdd(payment.Hash, payment))
                    throw new Exception("Payment with specified transaction hash already exists");
        }

        public void Remove(byte[] transactionHash)
        {
            if (transactionHash == null)
                throw new ArgumentNullException(nameof(transactionHash));

            lock (withdrawals)
                if (!withdrawals.Remove(transactionHash))
                    throw new Exception("Withdrawal with specified hash is not found");
        }

        public IEnumerable<Withdrawal> GetAll()
        {
            lock (withdrawals)
                return withdrawals.Values.Select(w => w);
        }

        public void AssignSignatures(byte[] txHash, List<DecoratedSignature> signatures)
        {
            lock (withdrawals)
            {
                var withdrawal = GetWithdrawal(txHash);
                if (withdrawal.Signatures != null)
                    throw new Exception("Transaction already assigned.");
                withdrawal.Signatures = signatures;
            }
        }

        public void Dispose()
        {
            if (submitTimer != null)
            {
                submitTimer.Stop();
                submitTimer.Dispose();
                submitTimer = null;
            }
        }

        #region private members

        private Dictionary<byte[], Withdrawal> withdrawals;
        private System.Timers.Timer submitTimer;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private void InitTimer()
        {
            submitTimer = new System.Timers.Timer();
            submitTimer.Interval = 10 * 1000;
            submitTimer.AutoReset = false;
            submitTimer.Elapsed += SubmitTimer_Elapsed;
            submitTimer.Start();
        }

        private void SubmitTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (Global.AppState.State == ApplicationState.Ready)
                    Cleanup();
                submitTimer.Start();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                Global.AppState.State = ApplicationState.Failed;
            }
        }

        public Withdrawal GetWithdrawal(byte[] transactionHash)
        {
            if (transactionHash == null)
                throw new ArgumentNullException(nameof(transactionHash));

            lock (withdrawals)
                return withdrawals.GetValueOrDefault(transactionHash);
        }

        public Withdrawal GetWithdrawal(long apex)
        {
            if (apex == default)
                throw new ArgumentNullException(nameof(apex));

            lock (withdrawals)
                return withdrawals.Values.FirstOrDefault(w => w.Apex == apex);
        }

        private void Cleanup()
        {
            lock (withdrawals)
            {
                var currentTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var expiredTransactions = withdrawals.Where(w => w.Value.IsExpired(currentTimeSeconds)).Select(w => w.Key);
            }
        }

        #endregion

    }
}
