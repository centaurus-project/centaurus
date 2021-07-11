using Centaurus.Exchange.Analytics;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PersistentStorage.Abstraction;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public partial class ExecutionContext : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        /// <param name="settings">Application config</param>
        /// <param name="storage">Permanent storage object</param>
        /// <param name="useLegacyOrderbook"></param>
        public ExecutionContext(Settings settings, IPersistentStorage storage, PaymentProviderFactoryBase paymentProviderFactory, bool useLegacyOrderbook = false)
        {
            PermanentStorage = storage ?? throw new ArgumentNullException(nameof(storage));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            PaymentProviderFactory = paymentProviderFactory ?? throw new ArgumentNullException(nameof(paymentProviderFactory));

            RoleManager = new RoleManager(
                (CentaurusNodeParticipationLevel)Settings.ParticipationLevel,
                Settings.AlphaPubKey == Settings.KeyPair.AccountId
                ? CentaurusNodeRole.Alpha
                : CentaurusNodeRole.Beta);

            ExtensionsManager = new ExtensionsManager(Settings.ExtensionsConfigFilePath);

            PersistenceManager = new PersistenceManager(this);
            QuantumProcessor = new QuantumProcessorsStorage();

            PendingUpdatesManager = new PendingUpdatesManager(this);
            PendingUpdatesManager.OnBatchSaved += PendingUpdatesManager_OnBatchSaved;

            QuantumStorage = new QuantumStorage();

            MessageHandlers = new MessageHandlers(this);

            InfoCommandsHandlers = new InfoCommandsHandlers(this);

            OutgoingMessageStorage = new OutgoingMessageStorage();

            OutgoingResultsStorage = new OutgoingResultsStorage(this);

            AppState = new StateManager(this);
            AppState.StateChanged += AppState_StateChanged;

            QuantumHandler = new QuantumHandler(this);

            ConnectionManager = new ConnectionManager(this);

            SubscriptionsManager = new SubscriptionsManager();
            InfoConnectionManager = new InfoConnectionManager(this);

            Catchup = new Catchup(this);

            this.useLegacyOrderbook = useLegacyOrderbook;

            Init();
        }

        /// <summary>
        /// Delay in seconds
        /// </summary>
        public const int MaxTxSubmitDelay = 5 * 60; //5 minutes

        readonly bool useLegacyOrderbook;

        private void Init()
        {
            DynamicSerializersInitializer.Init();

            //try to load last settings, we need it to know current auditors
            var lastHash = new byte[] { };
            var lastApex = PersistenceManager.GetLastApex();
            if (lastApex > 0)
            {
                var lastQuantum = PersistenceManager.GetQuantum(lastApex);

                lastHash = lastQuantum.Message.ComputeHash();
                var snapshot = PersistenceManager.GetSnapshot(lastApex);
                Setup(snapshot);
                if (IsAlpha)
                    AppState.State = ApplicationState.Rising;//Alpha should ensure that it has all quanta from auditors
                else
                    AppState.State = ApplicationState.Running;
            }
            else
                //if no snapshot, the application is in initialization state
                AppState.State = ApplicationState.WaitingForInit;

            var lastQuantumApex = lastApex < 0 ? 0 : lastApex;
            QuantumStorage.Init(lastQuantumApex, lastHash);
            QuantumHandler.Start();
        }

        public void Setup(Snapshot snapshot)
        {
            if (Exchange != null)
                Exchange.OnUpdates -= Exchange_OnUpdates;

            Constellation = snapshot.Settings;

            AccountStorage = new AccountStorage(snapshot.Accounts);

            Exchange?.Dispose(); Exchange = Exchange.RestoreExchange(snapshot.Settings.Assets, snapshot.Orders, IsAlpha, useLegacyOrderbook);

            PaymentProvidersManager?.Dispose(); PaymentProvidersManager = new PaymentProvidersManager(PaymentProviderFactory, Constellation.Providers);

            AuditResultManager?.Dispose(); AuditResultManager = new ResultManager(this);

            DisposeAnalyticsManager();

            AnalyticsManager = new AnalyticsManager(
                PermanentStorage,
                DepthsSubscription.Precisions.ToList(),
                Constellation.Assets.Skip(0).Select(a => a.Code).ToList(), //all but base asset
                snapshot.Orders.Select(o => o.Order.ToOrderInfo()).ToList()
            );

            AnalyticsManager.Restore(DateTime.UtcNow);
            AnalyticsManager.StartTimers();

            AnalyticsManager.OnError += AnalyticsManager_OnError;
            AnalyticsManager.OnUpdate += AnalyticsManager_OnUpdate;
            Exchange.OnUpdates += Exchange_OnUpdates;

            PerformanceStatisticsManager?.Dispose(); PerformanceStatisticsManager = new PerformanceStatisticsManager(this);
        }


        public void Dispose()
        {
            ExtensionsManager?.Dispose();
            PaymentProvidersManager?.Dispose();

            QuantumHandler.Dispose();
            AuditResultManager?.Dispose();
            DisposeAnalyticsManager(); 
            PerformanceStatisticsManager?.Dispose();
        }

        private void AppState_StateChanged(StateChangedEventArgs stateChangedEventArgs)
        {

            var state = stateChangedEventArgs.State;
            var prevState = stateChangedEventArgs.PrevState;
            if (state != ApplicationState.Ready && prevState == ApplicationState.Ready) //close all connections (except auditors)
                ConnectionManager.CloseAllConnections(false).Wait();
        }

        private void PendingUpdatesManager_OnBatchSaved(BatchSavedInfo batchInfo)
        {
            PerformanceStatisticsManager?.OnBatchSaved(batchInfo);
        }


        public PersistenceManager PersistenceManager { get; }

        public PendingUpdatesManager PendingUpdatesManager { get; }

        public bool IsAlpha => RoleManager.Role == CentaurusNodeRole.Alpha;

        public ExtensionsManager ExtensionsManager { get; }

        public QuantumProcessorsStorage QuantumProcessor { get; }

        public QuantumStorage QuantumStorage { get; }

        public RoleManager RoleManager { get; }

        public IPersistentStorage PermanentStorage { get; }

        public Settings Settings { get; }
        public PaymentProviderFactoryBase PaymentProviderFactory { get; }

        public StateManager AppState { get; }

        public QuantumHandler QuantumHandler { get; }

        public ConnectionManager ConnectionManager { get; }

        public SubscriptionsManager SubscriptionsManager { get; }

        public InfoConnectionManager InfoConnectionManager { get; }

        public Catchup Catchup { get; }

        public InfoCommandsHandlers InfoCommandsHandlers { get; }

        public MessageHandlers MessageHandlers { get; }

        public OutgoingMessageStorage OutgoingMessageStorage { get; }

        public OutgoingResultsStorage OutgoingResultsStorage { get; }

        public PaymentProvidersManager PaymentProvidersManager { get; private set; }

        public Exchange Exchange { get; private set; }

        public AccountStorage AccountStorage { get; private set; }

        public PerformanceStatisticsManager PerformanceStatisticsManager { get; private set; }

        public HashSet<int> AssetIds { get; private set; }

        public ResultManager AuditResultManager { get; private set; }

        public AnalyticsManager AnalyticsManager { get; private set; }

        public ConstellationSettings Constellation { get; private set; }

    }
}