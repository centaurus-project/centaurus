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
            InitTimers();

            LastPersistedApex = Context.DataProvider.GetLastApex();

            pendingUpdates = new UpdatesContainer(context);
            RegisterUpdates(pendingUpdates);
        }

        public event Action<BatchSavedInfo> OnBatchSaved;

        public System.Threading.SemaphoreSlim SyncRoot { get; } = new System.Threading.SemaphoreSlim(1);

        public void UpdateBatch(bool force = false)
        {
            if (force || IsBatchUpdateRequired())
            {
                SyncRoot.Wait();
                try
                {
                    if (pendingUpdates.QuantaCount < 1)
                        return;

                    logger.Info($"About to updated batch. Id: {pendingUpdates.Id}, apex range: {pendingUpdates.FirstApex}-{pendingUpdates.LastApex}, quanta: {pendingUpdates.QuantaCount}, effects: {pendingUpdates.EffectsCount}, affected accounts: {pendingUpdates.AffectedAccountsCount}.");

                    pendingUpdates.Complete(Context);

                    logger.Info($"Batch {pendingUpdates.Id} updated.");

                    pendingUpdates = new UpdatesContainer(Context, unchecked(pendingUpdates.Id + 1));
                    RegisterUpdates(pendingUpdates);
                }
                catch (Exception exc)
                {
                    if (Context.NodesManager.CurrentNode.State != State.Failed)
                    {
                        Context.NodesManager.CurrentNode.Failed(new Exception("Batch update failed.", exc));
                    }
                }
                finally
                {
                    SyncRoot.Release();
                }
            }
        }

        //last saved quantum apex
        public ulong LastPersistedApex { get; private set; }
        public void ApplyUpdates()
        {
            lock (applyUpdatesSyncRoot)
            {
                while (awaitedUpdates.TryPeek(out var updates) //verify that there are updates in the queue
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

                        LastPersistedApex = updates.LastApex;

                        var batchInfo = new BatchSavedInfo
                        {
                            SavedAt = DateTime.UtcNow,
                            QuantaCount = pendingUpdates.QuantaCount,
                            EffectsCount = pendingUpdates.EffectsCount,
                            ElapsedMilliseconds = sw.ElapsedMilliseconds,
                            FromApex = updates.FirstApex,
                            ToApex = updates.LastApex
                        };

                        OnBatchSaved?.Invoke(batchInfo);

                        logger.Info($"Batch {updates.Id} saved in {sw.ElapsedMilliseconds}.");
                    }
                    catch (Exception exc)
                    {
                        if (Context.NodesManager.CurrentNode.State != State.Failed)
                        {
                            Context.NodesManager.CurrentNode.Failed(new Exception("Saving failed.", exc));
                        }
                        return;
                    }
                }
            }
        }

        public void PersistPendingQuanta()
        {
            lock (applyUpdatesSyncRoot)
            {
                //if node stopped during rising than we don't need to persist pending quanta, it's already in db
                if (Context.NodesManager.CurrentNode.State == State.Rising)
                    return;
                var pendingQuanta = new List<PendingQuantumPersistentModel>();
                var hasMoreQuanta = true;
                while (hasMoreQuanta && awaitedUpdates.TryDequeue(out var updates))
                {
                    try
                    {
                        hasMoreQuanta = updates.GetPendingQuanta(out var currentPendingQuanta);
                        if (currentPendingQuanta.Count > 0)
                            pendingQuanta.AddRange(currentPendingQuanta.Select(q => new PendingQuantumPersistentModel
                            {
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

        public QuantumPersistentModel AddQuantum(QuantumProcessingItem quantumProcessingItem)
        {
            var qModel = new QuantumPersistentModel
            {
                Apex = quantumProcessingItem.Apex,
                Effects = quantumProcessingItem.Effects.Select(eg => new AccountEffects
                {
                    Account = eg.Account ?? new byte[32],
                    Effects = eg.RawEffects
                }).ToList(),
                RawQuantum = quantumProcessingItem.RawQuantum,
                TimeStamp = quantumProcessingItem.Quantum.Timestamp
            };

            pendingUpdates.AddQuantum(qModel, quantumProcessingItem.Effects.Sum(e => e.Effects.Effects.Count));
            pendingUpdates.AddQuantumRefs(quantumProcessingItem.Effects
                .Where(e => e.Account != null)
                .Select(eg => new QuantumRefPersistentModel
                {
                    Account = eg.Account,
                    Apex = quantumProcessingItem.Apex,
                    IsQuantumInitiator = eg.Account.Equals(quantumProcessingItem.Initiator?.Pubkey)
                }));

            if (quantumProcessingItem.HasSettingsUpdate)
                pendingUpdates.AddConstellation(Context.ConstellationSettingsManager.Current.ToPesrsistentModel());

            pendingUpdates.AddAffectedAccounts(quantumProcessingItem.Effects.Select(e => e.Account).Where(a => a != null));

            if (quantumProcessingItem.HasCursorUpdate)
                pendingUpdates.HasCursorUpdate = true;

            return qModel;
        }

        private ConcurrentQueue<UpdatesContainer> awaitedUpdates = new ConcurrentQueue<UpdatesContainer>();

        private UpdatesContainer pendingUpdates;

        private const int MaxQuantaCount = 50_000;
        private const int MaxSaveInterval = 5;

        private object applyUpdatesSyncRoot = new { };
        private Timer batchUpdateTimer = new Timer();
        private Timer batchSaveTimer = new Timer();
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

        private void InitTimers()
        {
            batchUpdateTimer.Interval = TimeSpan.FromMilliseconds(300).TotalMilliseconds;
            batchUpdateTimer.Elapsed += BatchUpdateTimer_Elapsed;
            batchUpdateTimer.AutoReset = false;
            batchUpdateTimer.Start();

            batchSaveTimer.Interval = TimeSpan.FromMilliseconds(300).TotalMilliseconds;
            batchSaveTimer.Elapsed += BatchSaveTimer_Elapsed;
            batchSaveTimer.AutoReset = false;
            batchSaveTimer.Start();
        }

        private void BatchUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (Context.NodesManager.CurrentNode.State == State.Running 
                    || Context.NodesManager.CurrentNode.State == State.Ready
                    || Context.NodesManager.CurrentNode.State == State.Chasing)
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
                batchUpdateTimer.Start();
            }
        }

        private void BatchSaveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                ApplyUpdates();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
            }
            finally
            {
                batchSaveTimer.Start();
            }
        }

        public void Dispose()
        {
            batchUpdateTimer.Stop();
            batchUpdateTimer.Elapsed -= BatchUpdateTimer_Elapsed;
            batchUpdateTimer.Dispose();

            batchSaveTimer.Stop();
            batchSaveTimer.Elapsed -= BatchUpdateTimer_Elapsed;
            batchSaveTimer.Dispose();

            buffer.Dispose();

            SyncRoot.Dispose();
        }
    }
}