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
        public WithdrawalStorage(IEnumerable<WithdrawalWrapper> withdrawals)
        {
            this.withdrawals = new Dictionary<byte[], WithdrawalWrapper>(new HashComparer());

            if (withdrawals == null)
                return;
            foreach (var payment in withdrawals)
                Add(payment);
            if (!EnvironmentHelper.IsTest)
                InitTimer();
        }

        public event Func<Dictionary<byte[], WithdrawalWrapper>, Task> OnSubmitTimer;

        public void Add(WithdrawalWrapper withdrawal)
        {
            if (withdrawal == null)
                throw new ArgumentNullException(nameof(withdrawal));

            syncRoot.Wait();
            try
            {
                if (!withdrawals.TryAdd(withdrawal.Hash, withdrawal))
                    throw new Exception("Payment with specified transaction hash already exists");
            }
            finally
            {
                syncRoot.Release();
            }
        }

        public void Remove(byte[] transactionHash)
        {
            if (transactionHash == null)
                throw new ArgumentNullException(nameof(transactionHash));

            syncRoot.Wait();
            try
            {
                if (!withdrawals.Remove(transactionHash, out var withdrawal))
                    throw new Exception("Withdrawal with specified hash is not found");
            }
            finally
            {
                syncRoot.Release();
            }
        }

        public IEnumerable<WithdrawalWrapper> GetAll()
        {
            syncRoot.Wait();
            try
            {
                return withdrawals.Values.Select(w => w);
            }
            finally
            {
                syncRoot.Release();
            }
        }

        public WithdrawalWrapper GetWithdrawal(byte[] transactionHash)
        {
            if (transactionHash == null)
                throw new ArgumentNullException(nameof(transactionHash));

            syncRoot.Wait();
            try
            {
                return withdrawals.GetValueOrDefault(transactionHash);
            }
            finally
            {
                syncRoot.Release();
            }
        }

        public WithdrawalWrapper GetWithdrawal(long apex)
        {
            if (apex == default)
                throw new ArgumentNullException(nameof(apex));

            syncRoot.Wait();
            try
            {
                return withdrawals.Values.FirstOrDefault(w => w.Apex == apex);
            }
            finally
            {
                syncRoot.Release();
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

        private Dictionary<byte[], WithdrawalWrapper> withdrawals;
        private System.Timers.Timer submitTimer;
        private SemaphoreSlim syncRoot = new SemaphoreSlim(1);

        private void InitTimer()
        {
            submitTimer = new System.Timers.Timer();
            submitTimer.Interval = 60 * 1000;
            submitTimer.AutoReset = false;
            submitTimer.Elapsed += SubmitTimer_Elapsed;
            submitTimer.Start();
        }

        private async void SubmitTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            await syncRoot.WaitAsync();
            try
            {
                if (OnSubmitTimer != null)
                    await OnSubmitTimer.Invoke(withdrawals);
            }
            finally
            {
                syncRoot.Release();
            }
        }

        #endregion

    }
}
