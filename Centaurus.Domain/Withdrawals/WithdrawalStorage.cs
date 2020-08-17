using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            withdrawals = new Dictionary<byte[], WithdrawalWrapper>(new HashComparer());

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
                if (!withdrawals.TryAdd(payment.TransactionHash, new WithdrawalWrapper(payment)))
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

        public Withdrawal GetWithdrawal(byte[] transactionHash)
        {
            return GetWithdrawalWrapper(transactionHash)?.Withdrawal;
        }

        public IEnumerable<Withdrawal> GetAll()
        {
            lock (withdrawals)
                return withdrawals.Values.Select(w => w.Withdrawal);
        }

        public void Submit(byte[] txHash, Transaction tx)
        {
            lock (withdrawals)
            {
                var withdrawal = GetWithdrawalWrapper(txHash);
                if (withdrawal.Tx != null)
                    throw new Exception("Transaction already assigned.");
                withdrawal.Tx = tx;
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
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private void InitTimer()
        {
            submitTimer = new System.Timers.Timer();
            submitTimer.Interval = 10 * 1000;
            submitTimer.AutoReset = false;
            submitTimer.Elapsed += SubmitTimer_Elapsed;
            submitTimer.Start();
        }

        private async void SubmitTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Global.AppState.State == ApplicationState.Running
                && !(await TrySubmit()))
                return; //errors occurred
            submitTimer.Start();
        }

        private WithdrawalWrapper GetWithdrawalWrapper(byte[] transactionHash)
        {
            if (transactionHash == null)
                throw new ArgumentNullException(nameof(transactionHash));

            lock (withdrawals)
                return withdrawals.GetValueOrDefault(transactionHash);
        }

        private async Task<bool> TrySubmit()
        {
            var tasks = new List<Task<SubmitTransactionResponse>>();
            lock (withdrawals)
            {
                foreach (var withdrawal in withdrawals.Values)
                {
                    if (withdrawal.Tx == null) //no transaction is not signed by auditors yet
                        break;
                    if (withdrawal.IsSubmitted) //transaction is submitted but not removed yet
                        continue;
                    withdrawal.IsSubmitted = true;
                    tasks.Add(Global.StellarNetwork.Server.SubmitTransaction(withdrawal.Tx));
                }
            }
            var result = await Task.WhenAll(tasks);
            var failedTxs = result
                .Where(r => r.Ledger == null)
                .Select(t => t.EnvelopeXdr)
                .ToArray();
            if (failedTxs.Count() > 0) //no ledger- no sequence update, need to restart to reset the vault account sequence
            {
                logger.Error($"Failed to submit next withdrawals : {string.Join(",\n", $"\"{failedTxs}\"")}");
                Global.AppState.State = ApplicationState.Failed;
                return false;
            }
            return true;
        }

        class WithdrawalWrapper
        {
            public WithdrawalWrapper(Withdrawal withdrawal)
            {
                Withdrawal = withdrawal;
            }

            public Withdrawal Withdrawal { get; }

            public Transaction Tx { get; set; }

            public bool IsSubmitted { get; set; }
        }

        #endregion

    }
}
