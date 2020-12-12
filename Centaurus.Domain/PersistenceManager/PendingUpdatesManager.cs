using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Centaurus.Domain
{
    public class PendingUpdatesManager
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public PendingUpdatesManager()
        {
            if (!EnvironmentHelper.IsTest)
                InitTimers();
        }

        public void AddEffects(MessageEnvelope quantum, Effect[] effects)
        {
            lock(updatesSyncRoot)
                updates.Add(quantum, effects);
        }

        public void OnSaveSuccess()
        {
            lock (timerSyncRoot)
            {
                snapshotIsInProgress = false;

                savingUpdatesTimoutTimer?.Stop();
                savingUpdatesRunTimer?.Start();
            }
        }

        public void OnSaveFailed(string reason)
        {
            lock (timerSyncRoot)
            {
                snapshotIsInProgress = false;

                savingUpdatesTimoutTimer?.Stop();
                savingUpdatesRunTimer?.Stop();

                logger.Error($"Snapshot failed. {reason}");
                Global.AppState.State = ApplicationState.Failed;
            }
        }

        private PendingUpdates updates = new PendingUpdates();

        private object updatesSyncRoot = new { };

        private System.Timers.Timer savingUpdatesTimoutTimer;

        private System.Timers.Timer savingUpdatesRunTimer;


        private bool snapshotIsInProgress = false;

        private object timerSyncRoot = new { };

        private void InitTimers()
        {
            //TODO: move interval to config
            savingUpdatesRunTimer = new System.Timers.Timer();
            savingUpdatesRunTimer.Interval = 5 * 1000;
            savingUpdatesRunTimer.AutoReset = false;
            savingUpdatesRunTimer.Elapsed += SaveUpdates;
            savingUpdatesRunTimer.Start();

            savingUpdatesTimoutTimer = new System.Timers.Timer();
            savingUpdatesTimoutTimer.Interval = 10 * 1000;
            savingUpdatesTimoutTimer.AutoReset = false;
            savingUpdatesTimoutTimer.Elapsed += (s, e) => OnSaveFailed("Snapshot save timed out.");
        }

        private void SaveUpdates(object sender, ElapsedEventArgs e)
        {
            lock (timerSyncRoot)
            {
                if (Global.AppState.State != ApplicationState.Ready)
                {
                    if (!snapshotIsInProgress)
                        savingUpdatesRunTimer.Start();
                    return;
                }

                snapshotIsInProgress = true;

                savingUpdatesTimoutTimer?.Start();

                _ = ApplyUpdates();
            }
        }

        private PendingUpdates RefreshUpdatesContainer()
        {
            lock (updatesSyncRoot)
            { 
                var pendingUpdates = updates;
                updates = new PendingUpdates();
                return pendingUpdates;
            }
        }

        private async Task ApplyUpdates()
        {
            try
            {
                await Global.PersistenceManager.ApplyUpdates(RefreshUpdatesContainer());
            }
            catch (Exception exc)
            {
                savingUpdatesTimoutTimer?.Stop();
                logger.Error(exc, "Error on saving updates.");
                Global.AppState.State = ApplicationState.Failed;
            }
        }
    }
}
