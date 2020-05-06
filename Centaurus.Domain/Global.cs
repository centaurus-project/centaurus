using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

            InitTimers();
        }

        public static void Setup(Snapshot snapshot)
        {
            //TODO: dispose objects if not null

            SnapshotManager = new SnapshotManager(OnSnapshotSuccess, OnSnapshotFailed);

            Constellation = snapshot.Settings;

            VaultAccount = new AccountData(snapshot.Settings.Vault, snapshot.VaultSequence);

            AccountStorage = new AccountStorage(snapshot.Accounts, Constellation.RequestRateLimits);

            Exchange = Exchange.RestoreExchange(snapshot.Settings.Assets, snapshot.Orders);

            AuditLedgerManager = new AuditLedgerManager();

            AuditResultManager = new AuditResultManager();

            WithdrawalStorage = new WithdrawalStorage(snapshot.Withdrawals);

            LedgerManager = new LedgerManager(snapshot.Ledger);

            ExtensionsManager = new ExtensionsManager();
            ExtensionsManager.RegisterAllExtensions().Wait();
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
        public static AccountData VaultAccount { get; private set; }
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

        private static PendingUpdates pendingUpdates = new PendingUpdates();

        public static void AddEffects(MessageEnvelope quantum, Effect[] effects)
        {
            pendingUpdates?.Add(quantum, effects);
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

        private static void OnSnapshotSuccess()
        {
            snapshotIsInProgress = false;

            snapshotTimoutTimer.Stop();
            snapshotRunTimer.Start();
        }

        private static void OnSnapshotFailed(string reason)
        {
            logger.Error($"Snapshot failed. {reason}");
            AppState.State = ApplicationState.Failed;
        }

        private static void SnapshotTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (AppState.State != ApplicationState.Ready)
            {
                snapshotRunTimer.Start();
                return;
            }

            //check if snapshot process is running
            while (snapshotIsInProgress)
                System.Threading.Thread.Sleep(100);

            var updates = pendingUpdates;
            pendingUpdates = new PendingUpdates();
            _ = SnapshotManager.ApplyUpdates(updates);

#if !DEBUG
            snapshotTimoutTimer.Start();
#endif
        }

        private static Timer snapshotTimoutTimer;

        private static Timer snapshotRunTimer;
    }
}
