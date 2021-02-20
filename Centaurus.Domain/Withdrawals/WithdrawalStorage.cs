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
        public WithdrawalStorage(IEnumerable<WithdrawalWrapper> withdrawals, bool startSubmitTimer = true)
        {
            this.withdrawals = new Dictionary<byte[], WithdrawalWrapper>(new HashComparer());

            if (withdrawals == null)
                return;
            foreach (var payment in withdrawals)
                Add(payment);
            if (startSubmitTimer)
                InitTimer();
        }

        public void Add(WithdrawalWrapper withdrawal)
        {
            if (withdrawal == null)
                throw new ArgumentNullException(nameof(withdrawal));

            lock (withdrawals)
            {
                if (!withdrawals.TryAdd(withdrawal.Hash, withdrawal))
                    throw new Exception("Payment with specified transaction hash already exists");
            }
        }

        public void Remove(byte[] transactionHash)
        {
            if (transactionHash == null)
                throw new ArgumentNullException(nameof(transactionHash));

            lock (withdrawals)
            {
                if (!withdrawals.Remove(transactionHash, out var withdrawal))
                    throw new Exception("Withdrawal with specified hash is not found");
            }
        }

        public IEnumerable<WithdrawalWrapper> GetAll()
        {
            lock (withdrawals)
                return withdrawals.Values.Select(w => w);
        }

        public WithdrawalWrapper GetWithdrawal(byte[] transactionHash)
        {
            if (transactionHash == null)
                throw new ArgumentNullException(nameof(transactionHash));

            lock (withdrawals)
                return withdrawals.GetValueOrDefault(transactionHash);
        }

        public WithdrawalWrapper GetWithdrawal(long apex)
        {
            if (apex == default)
                throw new ArgumentNullException(nameof(apex));

            lock (withdrawals)
                return withdrawals.Values.FirstOrDefault(w => w.Apex == apex);
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
        private static Logger logger = LogManager.GetCurrentClassLogger();

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
            try
            {
                if (Global.AppState.State == ApplicationState.Ready)
                    await Cleanup();
                submitTimer.Start();
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Error on withdrawal cleanup.");
                Global.AppState.State = ApplicationState.Failed;
            }
        }

        private async Task Cleanup()
        {
            byte[][] expiredTransactions = null;
            lock (withdrawals)
            {
                var currentTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                expiredTransactions = withdrawals.Where(w => w.Value.IsExpired(currentTimeSeconds)).Select(w => w.Key).ToArray();
            }

            if (expiredTransactions.Length < 1)
                return;

            //we must ignore all txs that was submitted. TxListener will handle submitted txs.
            var unhandledTxs = await GetUnhandledTx();
            foreach (var expiredTransaction in expiredTransactions.Where(tx => !unhandledTxs.Contains(tx, ByteArrayComparer.Default)))
                _ = Global.QuantumHandler.HandleAsync(new WithrawalsCleanupQuantum { ExpiredWithdrawal = expiredTransaction }.CreateEnvelope());
        }

        private async Task<List<byte[]>> GetUnhandledTx()
        {
            var retries = 1;
            while (true)
            {
                try
                {
                    var limit = 200;
                    var unhandledTxs = new List<byte[]>();
                    var pageResult = await Global.StellarNetwork.Server.GetTransactionsRequestBuilder(Global.Constellation.Vault.ToString(), Global.TxCursorManager.TxCursor, limit).Execute();
                    while (pageResult.Records.Count > 0)
                    {
                        unhandledTxs.AddRange(pageResult.Records.Select(r => ByteArrayExtensions.FromHexString(r.Hash)));
                        if (pageResult.Records.Count != limit)
                            break;
                        pageResult = await pageResult.NextPage();
                    }
                    return unhandledTxs;
                }
                catch
                {
                    if (retries == 5)
                        throw;
                    await Task.Delay(retries * 1000); 
                    retries++;
                }
            }
        }

        #endregion

    }
}
