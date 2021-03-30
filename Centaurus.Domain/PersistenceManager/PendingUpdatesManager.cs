using Centaurus.DAL;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Centaurus.Domain
{
    public class PendingUpdatesManager : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        //TODO: move interval to config
        /// <summary>
        /// Save interval in ms.
        /// </summary>
        public const int SaveInterval = 3 * 1000;

        public void Start()
        {
            if (!EnvironmentHelper.IsTest)
            {
                StartRefreshTimer();
                awaitedUpdates = new BlockingCollection<DiffObject>();
                savingOperation = Task.Factory.StartNew(async () => await StartUpdatesWorker(cancellationTokenSource.Token)).Unwrap();
            }
        }

        public void Stop()
        {
            //refresh container to put current updates into the awaited updates
            RefreshUpdatesContainer();

            if (!(awaitedUpdates == null || awaitedUpdates.IsAddingCompleted))
                awaitedUpdates.CompleteAdding();
            if (IsRunning)
            {
                //add some threshold just for case 
                var maxWaitTime = 60 * 1000 * 5;

                var sw = new Stopwatch();
                sw.Start();

                //wait while all pending updates are saved
                while (!savingOperation.IsCompleted && maxWaitTime > sw.ElapsedMilliseconds)
                    Thread.Sleep(100);

                if (!savingOperation.IsCompleted)
                {
                    logger.Error("Updates were not saved due to timeout.");
                    cancellationTokenSource.Cancel();
                }
                else
                    logger.Info("All updates are saved.");
            }
        }

        public bool IsRunning => !(savingOperation == null || savingOperation.IsCompleted);

        public DiffObject Current { get; private set; } = new DiffObject();

        public event Action<BatchSavedInfo> OnBatchSaved;

        public SemaphoreSlim UpdatesSyncRoot = new SemaphoreSlim(1);

        private System.Timers.Timer refreshUpdatesTimer;

        private BlockingCollection<DiffObject> awaitedUpdates;

        private object timerSyncRoot = new { };

        //need it to await all updates applied on a shutdown
        private Task savingOperation;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private void OnSaveFailed(string message, Exception exc = null)
        {
            lock (timerSyncRoot)
            {
                refreshUpdatesTimer?.Stop();

                var errorMessage = $"Snapshot failed. {message}";
                if (exc != null)
                    logger.Error(exc, errorMessage);
                else
                    logger.Error(errorMessage);
                Global.AppState.State = ApplicationState.Failed;
            }
        }

        private void StartRefreshTimer()
        {
            refreshUpdatesTimer = new System.Timers.Timer();
            refreshUpdatesTimer.Interval = SaveInterval;
            refreshUpdatesTimer.AutoReset = false;
            refreshUpdatesTimer.Elapsed += RefreshUpdates;
            refreshUpdatesTimer.Start();
        }

        private async Task StartUpdatesWorker(CancellationToken cancellationToken)
        {
            try
            {
                foreach (var updates in awaitedUpdates.GetConsumingEnumerable(cancellationToken))
                    await ApplyUpdates(updates);
            }
            catch (Exception exc)
            {
                if (exc is OperationCanceledException)
                    return;
                logger.Error(exc);
            }
        }

        private void RefreshUpdates(object sender, ElapsedEventArgs e)
        {
            lock (timerSyncRoot)
            {
                RefreshUpdatesContainer();

                if (Global.AppState.State == ApplicationState.Failed) //no need to start timer if application failed
                    return;
                refreshUpdatesTimer.Start();
            }
        }

        private void RefreshUpdatesContainer()
        {
            UpdatesSyncRoot.Wait();
            try
            {
                RefreshUpdatesUnlocked();
            }
            finally
            {
                UpdatesSyncRoot.Release();
            }
        }

        private void RefreshUpdatesUnlocked()
        {
            if (Current.Quanta.Count < 1)
                return;
            var pendingUpdates = Current;
            Current = new DiffObject();
            if (awaitedUpdates == null)
                return;

            awaitedUpdates.Add(pendingUpdates);
            if (Global.IsAlpha)
                QuantaThrottlingManager.Current.SetBatchQueueLength(awaitedUpdates.Count);
            else if (awaitedUpdates.Count >= 20 && Global.AppState.State != ApplicationState.Failed)
                OnSaveFailed($"Delayed updates queue ({awaitedUpdates.Count}) is too long.");
        }

        private async Task ApplyUpdates(DiffObject updates)
        {
            try
            {
                var sw = new Stopwatch();
                sw.Start();
                var retries = await Global.PersistenceManager.ApplyUpdates(updates);
                sw.Stop();

                var batchInfo = new BatchSavedInfo
                {
                    SavedAt = DateTime.UtcNow,
                    QuantaCount = updates.Quanta.Count,
                    EffectsCount = updates.EffectsCount,
                    ElapsedMilliseconds = sw.ElapsedMilliseconds,
                    Retries = retries
                };
                _ = Task.Factory.StartNew(() => OnBatchSaved?.Invoke(batchInfo));
            }
            catch (Exception exc)
            {
                //we need to cancel all pending updates
                cancellationTokenSource.Cancel();
                if (Global.AppState.State != ApplicationState.Failed)
                    OnSaveFailed("Error on saving updates.", exc);
            }
        }

        public void Dispose()
        {
            if (refreshUpdatesTimer != null)
            {
                refreshUpdatesTimer.Elapsed -= RefreshUpdates;
                refreshUpdatesTimer.Dispose();
                refreshUpdatesTimer = null;
            }
            awaitedUpdates?.Dispose();
            awaitedUpdates = null;
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
            UpdatesSyncRoot?.Dispose();
            UpdatesSyncRoot = null;
        }
    }
}
