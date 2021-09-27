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
    public partial class UpdatesManager : ContextualBase, IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public UpdatesManager(ExecutionContext context)
            : base(context)
        {
            InitTimer();

            LastSavedApex = Context.DataProvider.GetLastApex();

            pendingUpdates = new UpdatesContainer(context);
            RegisterUpdates(pendingUpdates);
        }

        public event Action<BatchSavedInfo> OnBatchSaved;

        public System.Threading.SemaphoreSlim SyncRoot { get; } = new System.Threading.SemaphoreSlim(1);

        public void UpdateBatch(bool force = false)
        {
            if (force || IsBatchUpdateRequired())
                try
                {
                    pendingUpdates.Complete(Context);

                    logger.Info($"Batch update. Id: {pendingUpdates.Id}, apex range: {pendingUpdates.FirstApex}-{pendingUpdates.LastApex}, quanta: {pendingUpdates.QuantaCount}, effects: {pendingUpdates.EffectsCount}");

                    pendingUpdates = new UpdatesContainer(Context, unchecked(pendingUpdates.Id + 1));
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

        public Task ApplyUpdatesAsync()
        {
            return Task.Factory.StartNew(() => ApplyUpdates());
        }

        //last saved quantum apex
        public ulong LastSavedApex { get; private set; }
        public void ApplyUpdates()
        {
            lock (applyUpdatesSyncRoot)
            {
                while (awaitedUpdates.TryPeek(out var updates) //verify that there is updates in the queue
                    && updates.IsCompleted //check if update is completed
                    && (updates.AreSignaturesCollected) //check if it's ready to be saved
                    && awaitedUpdates.TryDequeue(out _)) //remove it from the queue
                {
                    try
                    {
                        var sw = new Stopwatch();
                        sw.Start();
                        Context.DataProvider.SaveBatch(updates.GetUpdates());
                        sw.Stop();

                        LastSavedApex = updates.LastApex;

                        var batchInfo = new BatchSavedInfo
                        {
                            SavedAt = DateTime.UtcNow,
                            QuantaCount = pendingUpdates.QuantaCount,
                            EffectsCount = pendingUpdates.EffectsCount,
                            ElapsedMilliseconds = sw.ElapsedMilliseconds,
                            FromApex = updates.FirstApex,
                            ToApex = updates.LastApex
                        };

                        Task.Factory.StartNew(() => OnBatchSaved?.Invoke(batchInfo));

                        logger.Info($"Batch {updates.Id} saved in {sw.ElapsedMilliseconds}.");
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

        public void PersistPendingQuanta()
        {
            lock (applyUpdatesSyncRoot)
            {
                //if node stopped during rising than we don't need to persist pending quanta, it's already in db
                if (Context.StateManager.State == State.Rising)
                    return;
                var pendingQuanta = new List<PendingQuantumPersistentModel>();
                var hasMoreQuanta = true;
                while (hasMoreQuanta && awaitedUpdates.TryDequeue(out var updates))
                {
                    try
                    {
                        hasMoreQuanta = updates.GetPendingQuanta(out var currentPendingQuanta);
                        if (currentPendingQuanta.Count > 0)
                            pendingQuanta.AddRange(currentPendingQuanta.Select(q => new PendingQuantumPersistentModel { 
                                RawQuantum = q.RawQuantum,
                                Signatures = q.Signatures
                            }));
                    }
                    catch (Exception exc)
                    {
                        logger.Error(exc, "Error on obtaining pending quanta.");
                    }
                }
                var pendingQuantaModel = new PendingQuantaPersistentModel { Quanta = pendingQuanta };
                Context.DataProvider.SaveBatch(new List<IPersistentModel> { pendingQuantaModel });
            }
        }

        public QuantumPersistentModel AddQuantum(QuantaProcessingResult result)
        {
            var qModel = new QuantumPersistentModel
            {
                Apex = result.Apex,
                Effects = result.Effects.Select(eg => new AccountEffects
                {
                    Account = eg.Account ?? new byte[32],
                    Effects = eg.RawEffects
                }).ToList(),
                RawQuantum = result.RawQuantum,
                TimeStamp = result.Timestamp
            };

            pendingUpdates.AddQuantum(qModel, result.Effects.Sum(e => e.Effects.Effects.Count));
            pendingUpdates.AddQuantumRefs(result.Effects
                .Where(e => e.Account != null)
                .Select(eg => new QuantumRefPersistentModel
                {
                    Account = eg.Account,
                    Apex = result.Apex,
                    IsQuantumInitiator = eg.Account.Equals(result.Initiator)
                }));

            if (result.HasSettingsUpdate)
                pendingUpdates.AddConstellation(Context.Constellation.ToPesrsistentModel());

            pendingUpdates.AddAffectedAccounts(result.Effects.Select(e => e.Account).Where(a => a != null));

            if (result.HasCursorUpdate)
                pendingUpdates.HasCursorUpdate = true;

            return qModel;
        }

        private ConcurrentQueue<UpdatesContainer> awaitedUpdates = new ConcurrentQueue<UpdatesContainer>();

        private UpdatesContainer pendingUpdates;

        private const int MaxQuantaCount = 50_000;
        private const int MaxSaveInterval = 5;

        private object applyUpdatesSyncRoot = new { };
        private Timer saveTimer = new Timer();
        private XdrBufferFactory.RentedBuffer buffer = XdrBufferFactory.Rent(256 * 1024);


        private void RegisterUpdates(UpdatesContainer updatesContainer)
        {
            awaitedUpdates.Enqueue(updatesContainer);
            QuantaThrottlingManager.Current.SetBatchQueueLength(awaitedUpdates.Count);
        }

        private bool IsBatchUpdateRequired()
        {
            return pendingUpdates.QuantaCount > 0
                && (pendingUpdates.QuantaCount >= MaxQuantaCount || DateTime.UtcNow - pendingUpdates.InitDate > TimeSpan.FromSeconds(MaxSaveInterval));
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
            try
            {
                if (Context.StateManager?.State == State.Running || Context.StateManager?.State == State.Ready)
                {
                    SyncRoot.Wait();
                    try
                    {
                        UpdateBatch();
                    }
                    finally
                    {
                        SyncRoot.Release();
                    }
                    ApplyUpdatesAsync();
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

        public void Dispose()
        {
            saveTimer.Stop();
            saveTimer.Elapsed -= SaveTimer_Elapsed;
            saveTimer.Dispose();

            buffer.Dispose();

            SyncRoot.Dispose();
        }
    }
}