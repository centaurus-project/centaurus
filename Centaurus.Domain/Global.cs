using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Centaurus.DAL;
using Centaurus.DAL.Mongo;
using Centaurus.Models;
using NLog;

namespace Centaurus.Domain
{
    public static class Global
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Delay in seconds
        /// </summary>
        public const int MaxTxSubmitDelay = 5 * 60; //5 minutes

        /// <summary>
        /// Initializes Global object
        /// </summary>
        /// <param name="settings">Application config</param>
        /// <param name="storage">Permanent storage object</param>
        public static void Init(BaseSettings settings, BaseStorage storage)
        {
            ExtensionsManager = new ExtensionsManager();

            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            DynamicSerializersInitializer.Init();

            IsAlpha = Settings is AlphaSettings;

            PermanentStorage = storage;
            PermanentStorage.OpenConnection(settings.ConnectionString).Wait();

            StellarNetwork = new StellarNetwork(Settings.NetworkPassphrase, Settings.HorizonUrl);
            QuantumProcessor = new QuantumProcessorsStorage();

            AppState = IsAlpha ? new AlphaStateManager() : (StateManager)new AuditorStateManager();

            //try to load last settings, we need it to know current auditors
            var lastHash = new byte[] { };
            var lastApex = SnapshotManager.GetLastApex().Result;
            if (lastApex >= 0)
            {
                var lastQuantum = SnapshotManager.GetQuantum(lastApex).Result;
                lastHash = lastQuantum.Message.ComputeHash();
                logger.Trace($"Last hash is {Convert.ToBase64String(lastHash)}");
                var snapshot = SnapshotManager.GetSnapshot(lastApex).Result;
                Setup(snapshot);
                if (IsAlpha)
                    AppState.State = ApplicationState.Rising;//Alpha should ensure that it has all quanta from auditors
                else
                    AppState.State = ApplicationState.Running;
            }
            else
                //if no snapshot, the app is in init state
                AppState.State = ApplicationState.WaitingForInit;

            QuantumStorage = new QuantumStorage(lastApex < 0 ? 0 : lastApex, lastHash);

            QuantumHandler = new QuantumHandler(QuantumStorage.CurrentApex);

            pendingUpdates = new PendingUpdates();

            if (!EnvironmentHelper.IsTest)
                InitTimers();
        }

        public static void Setup(Snapshot snapshot)
        {
            SnapshotManager = new SnapshotManager(OnSnapshotSuccess, OnSnapshotFailed);

            Constellation = snapshot.Settings;

            AccountStorage = new AccountStorage(snapshot.Accounts, Constellation.RequestRateLimits);

            Exchange = Exchange.RestoreExchange(snapshot.Settings.Assets, snapshot.Orders);

            AuditLedgerManager?.Dispose(); AuditLedgerManager = new AuditLedgerManager();

            AuditResultManager?.Dispose(); AuditResultManager = new AuditResultManager();

            WithdrawalStorage?.Dispose(); WithdrawalStorage = new WithdrawalStorage(snapshot.Withdrawals, (!EnvironmentHelper.IsTest && IsAlpha));

            LedgerManager?.Dispose(); LedgerManager = new LedgerManager(snapshot.Ledger);

            ExtensionsManager?.Dispose(); ExtensionsManager = new ExtensionsManager();
            ExtensionsManager.RegisterAllExtensions();
        }

        public static Exchange Exchange { get; private set; }
        public static SnapshotManager SnapshotManager { get; private set; }

        private static ConstellationSettings constellation;
        public static ConstellationSettings Constellation
        {
            get
            {
                return constellation;
            }
            private set
            {
                constellation = value;
                AssetIds = constellation != null
                    ? new HashSet<int>(constellation.Assets.Select(a => a.Id).Concat(new int[] { 0 }))
                    : new HashSet<int>();
            }
        }
        public static QuantumStorage QuantumStorage { get; private set; }
        public static AccountStorage AccountStorage { get; private set; }
        public static WithdrawalStorage WithdrawalStorage { get; private set; }
        public static QuantumHandler QuantumHandler { get; private set; }
        public static AuditLedgerManager AuditLedgerManager { get; private set; }
        public static AuditResultManager AuditResultManager { get; private set; }
        public static LedgerManager LedgerManager { get; private set; }
        public static ExtensionsManager ExtensionsManager { get; private set; }
        public static StateManager AppState { get; private set; }
        public static QuantumProcessorsStorage QuantumProcessor { get; private set; }

        public static bool IsAlpha { get; private set; }
        public static BaseStorage PermanentStorage { get; private set; }
        public static BaseSettings Settings { get; private set; }
        public static StellarNetwork StellarNetwork { get; private set; }

        public static HashSet<int> AssetIds { get; private set; }

        private static PendingUpdates pendingUpdates;

        public static void AddEffects(MessageEnvelope quantum, Effect[] effects)
        {
            pendingUpdates.Add(quantum, effects);
        }

        static void InitTimers()
        {
            //TODO: move interval to config
            snapshotRunTimer = new Timer();
            snapshotRunTimer.Interval = 5 * 1000;
            snapshotRunTimer.AutoReset = false;
            snapshotRunTimer.Elapsed += SnapshotTimer_Elapsed;
            snapshotRunTimer.Start();

            snapshotTimoutTimer = new Timer();
            snapshotTimoutTimer.Interval = 10 * 1000;
            snapshotTimoutTimer.AutoReset = false;
            snapshotTimoutTimer.Elapsed += (s, e) => OnSnapshotFailed("Snapshot save timed out.");
        }

        private static bool snapshotIsInProgress = false;

        private static object timerSyncRoot = new { };

        private static void OnSnapshotSuccess()
        {
            lock (timerSyncRoot)
            {
                snapshotIsInProgress = false;

                snapshotTimoutTimer?.Stop();
                snapshotRunTimer?.Start();
            }
        }

        private static void OnSnapshotFailed(string reason)
        {
            lock (timerSyncRoot)
            {
                snapshotIsInProgress = false;

                snapshotTimoutTimer?.Stop();
                snapshotRunTimer?.Stop();

                logger.Error($"Snapshot failed. {reason}");
                AppState.State = ApplicationState.Failed;
            }
        }

        private static void SnapshotTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (timerSyncRoot)
            {
                if (AppState.State != ApplicationState.Ready)
                {
                    if (!snapshotIsInProgress)
                        snapshotRunTimer.Start();
                    return;
                }

                snapshotIsInProgress = true;

                _ = ApplyUpdates();

                snapshotTimoutTimer?.Start();
            }
        }

        private static async Task ApplyUpdates()
        {
            var updates = pendingUpdates;
            pendingUpdates = new PendingUpdates();
            await SnapshotManager.ApplyUpdates(updates);
        }

        private static Timer snapshotTimoutTimer;

        private static Timer snapshotRunTimer;
    }
}
