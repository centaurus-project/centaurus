using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.Xdr;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        }

        public event Action<BatchSavedInfo> OnBatchSaved;
        public void ApplyUpdates(bool force = false)
        {
            lock (syncRoot)
            {
                if (force || IsSaveRequired())
                    try
                    {
                        var sw = new Stopwatch();
                        sw.Start();
                        Context.PersistenceManager.ApplyUpdates(pendingUpdates.GetUpdates(Context));
                        sw.Stop();

                        lastSaveTime = DateTime.UtcNow;

                        var batchInfo = new BatchSavedInfo
                        {
                            SavedAt = lastSaveTime,
                            QuantaCount = pendingUpdates.QuantaCount,
                            EffectsCount = pendingUpdates.EffectsCount,
                            ElapsedMilliseconds = sw.ElapsedMilliseconds
                        };
                        Task.Factory.StartNew(() => OnBatchSaved?.Invoke(batchInfo));
                        pendingUpdates = new UpdatesContainer();
                    }
                    catch (Exception exc)
                    {
                        if (Context.AppState.State != ApplicationState.Failed)
                        {
                            logger.Error(exc, $"Saving failed.");
                            Context.AppState.State = ApplicationState.Failed;
                        }
                    }
            }
        }

        public void AddQuantum(ulong apex, MessageEnvelope messageEnvelope, List<Effect> effects, EffectsProof effectsProof)
        {
            var quantumModel = QuantumPersistentModelExtensions.ToPersistentModel(
                messageEnvelope,
                effects,
                effectsProof,
                buffer.Buffer);
            pendingUpdates.Batch.Add(quantumModel);
            pendingUpdates.Batch.AddRange(
                effects
                    .Where(e => e.Account > 0)
                    .GroupBy(e => e.Account)
                    .Select(e => new QuantumRefPersistentModel { AccountId = e.Key, Apex = apex })
                    .ToList()
            );
            pendingUpdates.EffectsCount += effects.Count;
            pendingUpdates.QuantaCount++;
        }

        private UpdatesContainer pendingUpdates = new UpdatesContainer();

        private const int MaxQuantaCount = 50_000;
        private const int MaxSaveInterval = 10;

        private DateTime lastSaveTime;
        private object syncRoot = new { };
        private Timer saveTimer = new Timer();
        private XdrBufferFactory.RentedBuffer buffer = XdrBufferFactory.Rent(256 * 1024);

        private bool IsSaveRequired()
        {
            return pendingUpdates.QuantaCount >= MaxQuantaCount || DateTime.UtcNow - lastSaveTime > TimeSpan.FromSeconds(MaxSaveInterval);
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
                    if (Context.AppState.State == ApplicationState.Running || Context.AppState.State == ApplicationState.Ready)
                    {
                        ApplyUpdates();
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
            lock(syncRoot)
            {
                saveTimer.Stop();
                saveTimer.Elapsed -= SaveTimer_Elapsed;
                saveTimer.Dispose();

                buffer.Dispose();
            }
        }

        class UpdatesContainer
        {

            public List<IPersistentModel> Batch { get; } = new List<IPersistentModel>();

            public HashSet<string> Cursors { get; } = new HashSet<string>();

            public HashSet<ulong> Accounts { get; } = new HashSet<ulong>();

            public int QuantaCount { get; set; }

            public int EffectsCount { get; set; }

            public List<IPersistentModel> GetUpdates(ExecutionContext context)
            {
                var updates = Batch.ToList();
                updates.AddRange(
                    Accounts.Select(a => context.AccountStorage.GetAccount(a).Account.ToPersistentModel()).Cast<IPersistentModel>().ToList()
                );
                updates.AddRange(
                    Cursors.Select(p => context.PaymentProvidersManager.GetManager(p).ToPersistentModel()).Cast<IPersistentModel>().ToList()
                );
                return updates;
            }
        }
    }
}
