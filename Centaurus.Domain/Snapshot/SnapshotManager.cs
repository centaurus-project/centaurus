using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class SnapshotManager
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly object syncRoot = new { };

        private Snapshot pendingSnapshot { get; set; }

        private Snapshot lastSnapshot;
        public Snapshot LastSnapshot
        {
            get
            {
                return lastSnapshot;
            }
            private set
            {
                lock (syncRoot)
                {
                    lastSnapshot = value;
                }
            }
        }

        /// <summary>
        /// Initiates snapshot manager
        /// </summary>
        /// <param name="_onSnapshotSuccess">The delegate that is called when the snapshot is successful</param>
        /// <param name="_onSnapshotFailed">The delegate that is called when the snapshot is failed</param>
        /// <param name="snapshot">Init snapshot</param>
        public SnapshotManager(Action _onSnapshotSuccess, Action<string> _onSnapshotFailed, Snapshot snapshot = null)
        {
            onSnapshotSuccess = _onSnapshotSuccess;
            onSnapshotFailed = _onSnapshotFailed;
            LastSnapshot = snapshot;
            pendingSnapshot = null;
        }

        /// <summary>
        /// Initiates snapshot process
        /// </summary>
        /// <returns>A snapshot object</returns>
        public Snapshot InitSnapshot()
        {
            lock (syncRoot)
            {
                if (pendingSnapshot != null)
                    throw new InvalidOperationException("Snapshot process is already initialized");

                var snapshot = new Snapshot();
                snapshot.Id = (LastSnapshot?.Id ?? 0) + 1;
                //if snapshot is valid, then snapshot quantum will be generated and current apex will be incremented
                snapshot.Apex = snapshot.Id != 1 ? Global.QuantumStorage.CurrentApex + 1 : 0;
                snapshot.Ledger = Global.LedgerManager.Ledger;
                snapshot.VaultSequence = Global.VaultAccount.SequenceNumber;
                snapshot.Settings = Global.Constellation;
                snapshot.Accounts = Global.AccountStorage.GetAll().ToList();
                snapshot.Orders = Global.Exchange.OrderMap.GetAllOrders().ToList();
                snapshot.Withdrawals = Global.WithdrawalStorage.GetAll().ToList();

                pendingSnapshot = snapshot;

                return pendingSnapshot;
            }
        }

        /// <summary>
        /// Saves pending snapshot. This operation is valid only for auditors
        /// </summary>
        public void SavePendingSnapshot()
        {
            if (Global.IsAlpha)
                throw new InvalidOperationException("Only an auditor can save pending snapshot directly");

            SnapshotValidated();
        }

        public void AbortPendingSnapshot()
        {
            lock (syncRoot)
            {
                pendingSnapshot = null;
            }
        }

        public static async Task<Snapshot> BuildGenesisSnapshot(ConstellationSettings settings, long ledger, long vaultSequence)
        {
            var snapshot = new Snapshot();
            snapshot.Id = 1;
            snapshot.Ledger = ledger;
            snapshot.Withdrawals = new List<PaymentRequestBase>();
            snapshot.Accounts = new List<Account>();
            snapshot.Orders = new List<Order>();
            snapshot.Settings = settings;
            snapshot.VaultSequence = vaultSequence;

            await SaveSnapshotInternal(snapshot.Id, XdrConverter.Serialize(snapshot));

            return snapshot;
        }

        public void SetResult(MessageEnvelope resultEnvelope)
        {
            lock (syncRoot)
            {
                var result = resultEnvelope.Message as ResultMessage;
                var snapshotQuantum = (SnapshotQuantum)result.OriginalMessage.Message;
                //TODO: cache hashes for pendingSnapshot
                if (pendingSnapshot == null || !ByteArrayPrimitives.Equals(pendingSnapshot.ComputeHash(), snapshotQuantum.Hash))
                {
                    logger.Info($"Finalize message received, but hash is invalid. Hash: {snapshotQuantum.Hash.ToHex()}");
                    return;
                }
                pendingSnapshot.Confirmation = resultEnvelope;
                if (MajorityHelper.HasMajority(resultEnvelope))
                    SnapshotValidated();
                else
                    onSnapshotSuccess?.Invoke();
            }
        }

        public async Task SavePendingQuantums()
        {
            IEnumerable<MessageEnvelope> pendingQuantums = Global.QuantumStorage.GetAllQuantums();
            if (LastSnapshot != null)
                pendingQuantums = pendingQuantums.Where(q => ((Quantum)q.Message).Apex > LastSnapshot.Apex);

            if (pendingQuantums.Count() > 0)
            {
                var pendingQuantumsSanapshot = new PendingQuanta { Quanta = new List<MessageEnvelope>(pendingQuantums) };
                await Global.SnapshotDataProvider.SavePendingQuanta(XdrConverter.Serialize(pendingQuantumsSanapshot));
            }
        }

        private void SnapshotValidated()
        {
            lock (syncRoot)
            {
                _ = SaveSnapshot(pendingSnapshot.Id, XdrConverter.Serialize(pendingSnapshot));

                LastSnapshot = pendingSnapshot;
                pendingSnapshot = null;

                onSnapshotSuccess?.Invoke();
            }
        }

        //only one thread can save snapshots. We need make sure that previous snapshot is permanent
        private async Task SaveSnapshot(ulong id, byte[] snapshot)
        {
            try
            {
                await SaveSnapshotInternal(id, snapshot);
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Unable to save snapshot");
                onSnapshotFailed?.Invoke("Unable to save snapshot");
            }
        }

        private Action onSnapshotSuccess;
        private Action<string> onSnapshotFailed;


        private static SemaphoreSlim saveSnapshotSemaphore = new SemaphoreSlim(1);

        private static async Task SaveSnapshotInternal(ulong id, byte[] snapshot)
        {
            await saveSnapshotSemaphore.WaitAsync();
            try
            {
                await Global.SnapshotDataProvider.SaveSnapshot(id, snapshot);
            }
            finally
            {
                saveSnapshotSemaphore.Release();
            }
        }
    }
}
