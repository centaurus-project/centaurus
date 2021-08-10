using Centaurus.Client;
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
        public ExecutionContext(Settings settings, IPersistentStorage storage, PaymentProvidersFactoryBase paymentProviderFactory, OutgoingConnectionFactoryBase connectionFactory, bool useLegacyOrderbook = false)
        {
            PermanentStorage = storage ?? throw new ArgumentNullException(nameof(storage));
            PermanentStorage.Connect(settings.ConnectionString);

            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            PaymentProviderFactory = paymentProviderFactory ?? throw new ArgumentNullException(nameof(paymentProviderFactory));

            RoleManager = new RoleManager((CentaurusNodeParticipationLevel)Settings.ParticipationLevel);

            ExtensionsManager = new ExtensionsManager(Settings.ExtensionsConfigFilePath);

            PersistenceManager = new PersistenceManager(this);

            QuantumProcessor = new QuantumProcessorsStorage(this);

            PendingUpdatesManager = new PendingUpdatesManager(this);
            PendingUpdatesManager.OnBatchSaved += PendingUpdatesManager_OnBatchSaved;

            QuantumStorage = new QuantumStorage();

            MessageHandlers = new MessageHandlers(this);

            InfoCommandsHandlers = new InfoCommandsHandlers(this);

            QuantumHandler = new QuantumHandler(this);

            IncomingConnectionManager = new IncomingConnectionManager(this);

            OutgoingConnectionManager = new OutgoingConnectionManager(this, connectionFactory);

            SubscriptionsManager = new SubscriptionsManager();
            InfoConnectionManager = new InfoConnectionManager(this);

            Catchup = new Catchup(this);

            this.useLegacyOrderbook = useLegacyOrderbook;

            DynamicSerializersInitializer.Init();

            var state = State.Running;
            var lastSnapshot = PersistenceManager.GetLastSnapshot();
            if (lastSnapshot != null)
            {
                Setup(lastSnapshot);
                if (IsAlpha) //TODO: all auditors should walk trough rising routine
                    state = State.Rising;//Alpha should ensure that it has all quanta from auditors
            }

            StateManager = new StateManager(this, state);
            StateManager.StateChanged += AppState_StateChanged;

            if (lastSnapshot == null)
            {
                EstablishOutgoingConnections();
            }
            QuantumStorage.Init(lastSnapshot?.Apex ?? 0, lastSnapshot?.LastHash ?? new byte[] { });
            QuantumHandler.Start();
        }

        /// <summary>
        /// Delay in seconds
        /// </summary>
        public const int MaxTxSubmitDelay = 5 * 60; //5 minutes

        readonly bool useLegacyOrderbook;

        public void Setup(Snapshot snapshot)
        {
            if (Exchange != null)
                Exchange.OnUpdates -= Exchange_OnUpdates;

            Constellation = snapshot.Settings;

            SetRole();

            AccountStorage = new AccountStorage(snapshot.Accounts);

            Exchange?.Dispose(); Exchange = Exchange.RestoreExchange(snapshot.Settings.Assets, snapshot.Orders, IsAlpha, useLegacyOrderbook);

            SetupPaymentProviders();

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

            IncomingConnectionManager.CleanupAuditorConnections();

            EstablishOutgoingConnections();
        }

        private void EstablishOutgoingConnections()
        {
            var auditors = Constellation != null
                ? Constellation.Auditors.Select(a => new Settings.Auditor(a.PubKey, a.Address)).ToList()
                : Settings.GenesisAuditors.ToList();
            OutgoingConnectionManager.Connect(auditors);
        }

        private void SetRole()
        {
            if (RoleManager.ParticipationLevel == CentaurusNodeParticipationLevel.Auditor)
            {
                if (Constellation.Alpha.Equals((RawPubKey)Settings.KeyPair))
                    throw new InvalidOperationException("Server with Auditor level cannot be set as Alpha.");
                return;
            }
            if (Constellation.Alpha.Equals((RawPubKey)Settings.KeyPair))
                RoleManager.SetRole(CentaurusNodeRole.Alpha);
            else
                RoleManager.SetRole(CentaurusNodeRole.Beta);
        }

        private void SetupPaymentProviders()
        {
            PaymentProvidersManager?.Dispose();
            PaymentProvidersManager = new PaymentProvidersManager(PaymentProviderFactory, Constellation.Providers.Select(p => p.ToProviderModel()).ToList(), Settings.PaymentConfigPath);

            foreach (var paymentProvider in PaymentProvidersManager.GetAll())
            {
                paymentProvider.OnPaymentCommit += PaymentProvider_OnPaymentCommit;
            }
        }

        private void PaymentProvider_OnPaymentCommit(PaymentProviderBase paymentProvider, PaymentProvider.Models.DepositNotificationModel notification)
        {
            if (!IsAlpha)
                return;

            QuantumHandler.HandleAsync(new DepositQuantum { Source = notification.ToDomainModel() });
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
            if (state != State.Ready && prevState == State.Ready) //close all connections (except auditors)
                IncomingConnectionManager.CloseAllConnections(false).Wait();
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

        public PaymentProvidersFactoryBase PaymentProviderFactory { get; }

        public StateManager StateManager { get; }

        public QuantumHandler QuantumHandler { get; }

        public IncomingConnectionManager IncomingConnectionManager { get; }

        public OutgoingConnectionManager OutgoingConnectionManager { get; }

        public SubscriptionsManager SubscriptionsManager { get; }

        public InfoConnectionManager InfoConnectionManager { get; }

        public Catchup Catchup { get; }

        public InfoCommandsHandlers InfoCommandsHandlers { get; }

        public MessageHandlers MessageHandlers { get; }

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