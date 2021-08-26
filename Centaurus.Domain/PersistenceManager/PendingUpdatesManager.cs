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

            LastSavedApex = Context.PersistenceManager.GetLastApex();

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
                        ApplyUpdatesAsync();
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

        public Task ApplyUpdatesAsync(bool force = false)
        {
            return Task.Factory.StartNew(() => ApplyUpdates(force));
        }

        //last saved quantum apex
        public ulong LastSavedApex {get; private set; }

        public void ApplyUpdates(bool force = false)
        {
            lock (syncRoot)
            {
                var updates = GetFirstUpdates();
                while (updates != null && updates.IsCompleted && (updates.AreSignaturesCollected || force))
                {
                    try
                    {
                        var sw = new Stopwatch();
                        sw.Start();
                        Context.PersistenceManager.ApplyUpdates(updates.GetUpdates());
                        sw.Stop();

                        LastSavedApex = updates.LastApex;

                        var batchInfo = new BatchSavedInfo
                        {
                            SavedAt = lastUpdateTime,
                            QuantaCount = pendingUpdates.QuantaCount,
                            EffectsCount = pendingUpdates.EffectsCount,
                            ElapsedMilliseconds = sw.ElapsedMilliseconds
                        };
                        Task.Factory.StartNew(() => OnBatchSaved?.Invoke(batchInfo));
                        RemoveUpdates(updates.Id);

                        Console.WriteLine($"Batch {updates.Id} saved in {sw.ElapsedMilliseconds}.");

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
        }

        public void AddSignatures(uint updatesId, ulong apex, List<AuditorResult> signatures)
        {
            lock (awaitedUpdatesSyncRoot)
            {
                if (!awaitedUpdates.TryGetValue(updatesId, out var updates))
                    throw new Exception($"Updates with id {updatesId} is not registered.");
                updates.AddSignatures(apex, signatures);
                if (updates.AreSignaturesCollected)
                    ApplyUpdatesAsync();
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
                TimeStamp = result.Timestamp,
                Signatures = new List<SignatureModel> { result.CurrentNodeSignature.ToPersistenModel() }
            };

            if (pendingUpdates.FirstApex == 0)
                pendingUpdates.FirstApex = result.Apex;

            pendingUpdates.LastApex = result.Apex;

            pendingUpdates.AddQuantum(qModel, result.Effects.Sum(e => e.Effects.Effects.Count));
            pendingUpdates.AddQuantumRefs(result.Effects.Select(eg => new QuantumRefPersistentModel
            {
                AccountId = eg.Account,
                Apex = result.Apex,
                IsQuantumInitiator = eg.Account == result.Initiator
            }));

            if (result.HasSettingsUpdate)
                pendingUpdates.AddConstellation(Context.Constellation.ToPesrsistentModel());

            pendingUpdates.AddAffectedAccounts(result.AffectedAccounts.Keys);

            if (result.HasCursorUpdate)
                pendingUpdates.HasCursorUpdate = true;

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
                    if (Context.StateManager?.State == State.Running || Context.StateManager?.State == State.Ready)
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

            public bool IsCompleted { get; private set; }

            private object batchSyncRoot = new { };
            private List<IPersistentModel> batch = new List<IPersistentModel>();

            private HashSet<ulong> accounts = new HashSet<ulong>();

            public bool HasCursorUpdate { get; set; }

            public int QuantaCount { get; private set; }

            public int EffectsCount { get; private set; }

            public ulong FirstApex;
            public ulong LastApex;

            public void AddAffectedAccounts(IEnumerable<ulong> accounts)
            {
                this.accounts.UnionWith(accounts);
            }

            public void AddQuantum(QuantumPersistentModel quantum, int effectsCount)
            {
                lock (pendingQuantaSyncRoot)
                {
                    pendingQuanta.Add(quantum.Apex, quantum);
                    QuantaCount++;
                    EffectsCount += effectsCount;
                    LastApex = quantum.Apex;
                }
            }

            public void AddConstellation(SettingsPersistentModel persistentModel)
            {
                lock (batchSyncRoot)
                    batch.Add(persistentModel);
            }

            public void AddQuantumRefs(IEnumerable<QuantumRefPersistentModel> quantumRefs)
            {
                lock (batchSyncRoot)
                    batch.AddRange(quantumRefs);
            }

            public void Complete(ExecutionContext context)
            {
                if (IsCompleted)
                    throw new InvalidOperationException("Already completed.");

                lock (batchSyncRoot)
                {
                    if (accounts.Count > 0)
                    {
                        var accountModels = accounts.Select(a => context.AccountStorage.GetAccount(a).ToPersistentModel()).Cast<IPersistentModel>().ToList();
                        batch.AddRange(accountModels);
                    }

                    if (HasCursorUpdate)
                        batch.Add(new CursorsPersistentModel { Cursors = context.PaymentProvidersManager.GetAll().ToDictionary(k => k.Id, v => v.Cursor) });

                    IsCompleted = true;
                }
            }

            private object pendingQuantaSyncRoot = new { };
            private Dictionary<ulong, QuantumPersistentModel> pendingQuanta { get; } = new Dictionary<ulong, QuantumPersistentModel>();

            public void AddSignatures(ulong apex, List<AuditorResult> signatures)
            {
                //if (LastAdded + 1 != apex)
                //    Console.WriteLine($"Adding {apex}, prev was {LastAdded}");
                LastAdded = apex;
                lock (pendingQuantaSyncRoot)
                {
                    if (!pendingQuanta.Remove(apex, out var quantum))
                        throw new InvalidOperationException($"Unable to find quantum with {apex} apex.");
                    quantum.Signatures = signatures.Select(s => s.Signature.ToPersistenModel()).ToList();

                    lock (batchSyncRoot)
                        batch.Add(quantum);
                }
            }

            static ulong LastAdded;

            internal List<IPersistentModel> GetUpdates()
            {
                lock (batchSyncRoot)
                    return batch;
            }

            public bool AreSignaturesCollected
            {
                get
                {
                    lock (pendingQuantaSyncRoot)
                        return pendingQuanta.Count == 0;
                }
            }
        }
    }
}
