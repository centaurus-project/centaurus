using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace Centaurus.Domain
{
    public class PendingUpdatesManager : ContextualBase, IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public PendingUpdatesManager(ExecutionContext context)
            : base(context)
        {
            InitTimer();

            pendingUpdates = new UpdatesContainer();
            RegisterUpdates(pendingUpdates);
        }

        public event Action<BatchSavedInfo> OnBatchSaved;
        public void UpdateBatch(bool force = false)
        {
            lock (syncRoot)
            {
                if (force || IsSaveRequired())
                    try
                    {
                        pendingUpdates.Complete(Context);
                        lastUpdateTime = DateTime.UtcNow;

                        pendingUpdates = new UpdatesContainer(unchecked(pendingUpdates.Id + 1));
                        RegisterUpdates(pendingUpdates);
                    }
                    catch (Exception exc)
                    {
                        if (Context.StateManager.State != State.Failed)
                        {
                            Context.StateManager.Failed(new Exception("Batch update failed.", exc));
                        }
                    }
            }
        }

        public void ApplyUpdates(bool force = false)
        {
            var updates = GetFirstUpdates();
            while (updates != null && updates.IsCompleted && (updates.AreSignaturesCollected || force))
            {
                try
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    Context.PersistenceManager.ApplyUpdates(updates.UpdateModels);
                    sw.Stop();

                    var batchInfo = new BatchSavedInfo
                    {
                        SavedAt = lastUpdateTime,
                        QuantaCount = pendingUpdates.QuantaCount,
                        EffectsCount = pendingUpdates.EffectsCount,
                        ElapsedMilliseconds = sw.ElapsedMilliseconds
                    };
                    Task.Factory.StartNew(() => OnBatchSaved?.Invoke(batchInfo));
                    RemoveUpdates(updates.Id);
                    updates = GetFirstUpdates();
                }
                catch (Exception exc)
                {
                    updates = null;
                    if (Context.StateManager.State != State.Failed)
                    {
                        Context.StateManager.Failed(new Exception("Saving failed.", exc));
                    }
                }
            }
        }

        public void AddSignatures(uint updatesId, ulong apex, List<AuditorResultMessage> signatures)
        {
            lock (awaitedUpdatesSyncRoot)
            {
                if (!awaitedUpdates.TryGetValue(updatesId, out var updates))
                    throw new Exception($"Updates with id {updatesId} is not registered.");
                updates.AddSignatures(apex, signatures);
                if (updates.AreSignaturesCollected)
                    ApplyUpdates();
            }
        }

        private void RegisterUpdates(UpdatesContainer updatesContainer)
        {
            lock (awaitedUpdatesSyncRoot)
                awaitedUpdates.Add(updatesContainer.Id, updatesContainer);
        }

        private void RemoveUpdates(uint id)
        {
            lock (awaitedUpdatesSyncRoot)
                if (!awaitedUpdates.Remove(id))
                    throw new Exception("Unable to remove updates from pending collection.");
        }

        private UpdatesContainer GetFirstUpdates()
        {
            lock (awaitedUpdatesSyncRoot)
                return awaitedUpdates.FirstOrDefault().Value;
        }

        public uint AddQuantum(QuantaProcessingResult result)
        {
            var qModel = new QuantumPersistentModel
            {
                Apex = result.Apex,
                Effects = result.Effects.Select(eg => new AccountEffects
                {
                    Account = eg.Account,
                    Effects = eg.RawEffects
                }).ToList(),
                RawQuantum = result.RawQuantum,
                TimeStamp = result.Timestamp
            };

            qModel.Signatures = new List<SignatureModel> { result.CurrentNodeSignature.ToPersistenModel() };

            pendingUpdates.AddQuantum(qModel);

            if (result.HasCursorUpdate)
                pendingUpdates.HasCursorUpdate = true;

            if (result.HasSettingsUpdate)
                pendingUpdates.Batch.Add(Context.Constellation.ToPesrsistentModel());

            pendingUpdates.Batch.AddRange(result.Effects.Select(eg => new QuantumRefPersistentModel
            {
                AccountId = eg.Account,
                Apex = result.Apex,
                IsQuantumInitiator = eg.Account == result.Initiator
            }));

            pendingUpdates.Accounts.UnionWith(result.AffectedAccounts.Keys);

            pendingUpdates.EffectsCount += result.Effects.Count;
            pendingUpdates.QuantaCount++;

            return pendingUpdates.Id;
        }

        private object awaitedUpdatesSyncRoot = new { };
        SortedDictionary<uint, UpdatesContainer> awaitedUpdates = new SortedDictionary<uint, UpdatesContainer>();

        private UpdatesContainer pendingUpdates;

        private const int MaxQuantaCount = 50_000;
        private const int MaxSaveInterval = 10;

        private DateTime lastUpdateTime;
        private object syncRoot = new { };
        private Timer saveTimer = new Timer();
        private XdrBufferFactory.RentedBuffer buffer = XdrBufferFactory.Rent(256 * 1024);

        private bool IsSaveRequired()
        {
            return pendingUpdates.QuantaCount > 0
                && (pendingUpdates.QuantaCount >= MaxQuantaCount || DateTime.UtcNow - lastUpdateTime > TimeSpan.FromSeconds(MaxSaveInterval));
        }

        private void InitTimer()
        {
            saveTimer.Interval = TimeSpan.FromSeconds(5).TotalMilliseconds;
            saveTimer.Elapsed += SaveTimer_Elapsed;
            saveTimer.AutoReset = false;
            saveTimer.Start();
        }

        private void SaveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (syncRoot)
            {
                try
                {
                    if (Context.StateManager.State == State.Running || Context.StateManager.State == State.Ready)
                    {
                        UpdateBatch();
                    }
                }
                catch (Exception exc)
                {
                    logger.Error(exc);
                }
                finally
                {
                    saveTimer.Start();
                }
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                saveTimer.Stop();
                saveTimer.Elapsed -= SaveTimer_Elapsed;
                saveTimer.Dispose();

                buffer.Dispose();
            }
        }

        class UpdatesContainer
        {
            public UpdatesContainer(uint id = 0)
            {
                Id = id;
            }

            public uint Id { get; }

            public List<IPersistentModel> Batch { get; } = new List<IPersistentModel>();

            public bool HasCursorUpdate { get; set; }

            public HashSet<ulong> Accounts { get; } = new HashSet<ulong>();

            public int QuantaCount { get; set; }

            public int EffectsCount { get; set; }

            public List<IPersistentModel> UpdateModels { get; private set; }

            public void AddQuantum(QuantumPersistentModel quantum)
            {
                Batch.Add(quantum);
                lock (syncRoot)
                    PendingQuanta.Add(quantum.Apex, quantum);
            }

            public void Complete(ExecutionContext context)
            {
                if (UpdateModels != null)
                    throw new InvalidOperationException("Already completed.");

                var updates = Batch.ToList();
                if (Accounts.Count > 0)
                    updates.AddRange(
                        Accounts.Select(a => context.AccountStorage.GetAccount(a).Account.ToPersistentModel()).Cast<IPersistentModel>().ToList()
                    );

                if (HasCursorUpdate)
                    updates.Add(new CursorsPersistentModel { Cursors = context.PaymentProvidersManager.GetAll().ToDictionary(k => k.Id, v => v.Cursor) });

                UpdateModels = updates;
            }

            private object syncRoot = new { };
            public Dictionary<ulong, QuantumPersistentModel> PendingQuanta { get; } = new Dictionary<ulong, QuantumPersistentModel>();

            public void AddSignatures(ulong apex, List<AuditorResultMessage> signatures)
            {
                lock (syncRoot)
                {
                    if (!PendingQuanta.Remove(apex, out var quantum))
                        throw new InvalidOperationException($"Unable to find quantum with {apex} apex.");
                    quantum.Signatures = signatures.Select(s => s.Signature.ToPersistenModel()).ToList();
                }
            }

            public bool AreSignaturesCollected
            {
                get
                {
                    lock (syncRoot)
                        return PendingQuanta.Count == 0;
                }
            }

            public bool IsCompleted => UpdateModels != null;
        }
    }
}
