using System;
using System.Collections.Generic;
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
        public static void Init(BaseSettings settings, BaseStorage storage)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            IsAlpha = Settings is AlphaSettings;

            PermanentStorage = storage;
            PermanentStorage.OpenConnection(settings.ConnectionString).Wait();

            StellarNetwork = new StellarNetwork(Settings.NetworkPassphrase, Settings.HorizonUrl);
            QuantumProcessor = new QuantumProcessorsStorage();

            AppState = IsAlpha ? new AlphaStateManager() : (StateManager)new AuditorStateManager();

            if (IsAlpha)
                InitTimers();
        }

        public static void Setup(Snapshot snapshot, IEnumerable<MessageEnvelope> quanta = null)
        {
            //TODO: dispose objects if not null

            SnapshotManager = new SnapshotManager(OnSnapshotSuccess, OnSnapshotFailed);

            Constellation = snapshot.Settings;

            QuantumStorage = new QuantumStorage(snapshot.Apex);

            PendingUpdates = new PendingUpdates();

            VaultAccount = new AccountData(snapshot.Settings.Vault, snapshot.VaultSequence);

            AccountStorage = new AccountStorage(snapshot.Accounts);

            Exchange = Exchange.RestoreExchange(snapshot.Settings.Assets, snapshot.Orders);

            AuditLedgerManager = new AuditLedgerManager();

            AuditResultManager = new AuditResultManager();

            WithdrawalStorage = new WithdrawalStorage(snapshot.Withdrawals);

            QuantumHandler = IsAlpha ? (BaseQuantumHandler)new AlphaQuantumHandler(quanta) : new AuditorQuantumHandler();

            LedgerManager = new LedgerManager(snapshot.Ledger);

            AppState.State = ApplicationState.Running;
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
        public static PendingUpdates PendingUpdates { get; private set; }
        public static AccountData VaultAccount { get; private set; }
        public static AccountStorage AccountStorage { get; private set; }
        public static WithdrawalStorage WithdrawalStorage { get; private set; }
        public static BaseQuantumHandler QuantumHandler { get; private set; }
        public static AuditLedgerManager AuditLedgerManager { get; private set; }
        public static AuditResultManager AuditResultManager { get; private set; }
        public static LedgerManager LedgerManager { get; private set; }
        public static StateManager AppState { get; private set; }
        public static QuantumProcessorsStorage QuantumProcessor { get; private set; }

        public static bool IsAlpha { get; private set; }
        public static BaseStorage PermanentStorage { get; private set; }
        public static BaseSettings Settings { get; private set; }
        public static StellarNetwork StellarNetwork { get; private set; }

        public static HashSet<int> AssetIds { get; private set; }

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
            if (Settings is AuditorSettings)
                return;

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

            var updates = PendingUpdates;
            PendingUpdates = new PendingUpdates();
           _ = SnapshotManager.SaveSnapshot(updates);

#if !DEBUG
            snapshotTimoutTimer.Start();
#endif
        }

        private static Timer snapshotTimoutTimer;

        private static Timer snapshotRunTimer;
    }
}
