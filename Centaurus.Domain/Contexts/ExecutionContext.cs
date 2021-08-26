using Centaurus.Client;
using Centaurus.Exchange.Analytics;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PersistentStorage.Abstraction;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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

            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            PermanentStorage = storage ?? throw new ArgumentNullException(nameof(storage));
            PermanentStorage.Connect(GetAbsolutePath(Settings.ConnectionString));

            PaymentProviderFactory = paymentProviderFactory ?? throw new ArgumentNullException(nameof(paymentProviderFactory));

            RoleManager = new RoleManager((CentaurusNodeParticipationLevel)Settings.ParticipationLevel);

            ExtensionsManager = new ExtensionsManager(GetAbsolutePath(Settings.ExtensionsConfigFilePath));

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

            StateManager = new StateManager(this);
            StateManager.StateChanged += AppState_StateChanged;

            this.useLegacyOrderbook = useLegacyOrderbook;

            DynamicSerializersInitializer.Init();

            var lastSnapshot = PersistenceManager.GetLastSnapshot();
            var state = State.Undefined;
            if (lastSnapshot != null)
            {
                Setup(lastSnapshot);
                state = IsAlpha ? State.Rising : State.Running;
            }
            else if (lastSnapshot == null)
            {
                //establish connection with genesis auditors
                EstablishOutgoingConnections();
            }

            StateManager.Init(state);

            QuantumStorage.Init(lastSnapshot?.Apex ?? 0, lastSnapshot?.LastHash ?? new byte[] { });
            QuantumHandler.Start();
        }

        private string GetAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;
            return Path.IsPathRooted(path) 
                ? path 
                : Path.GetFullPath(Path.Combine(Settings.CWD, path.Trim('.').Trim('\\').Trim('/')));
        }

        readonly bool useLegacyOrderbook;

        public void Setup(Snapshot snapshot)
        {
            if (Exchange != null)
                Exchange.OnUpdates -= Exchange_OnUpdates;

            Constellation = snapshot.Settings;

            AuditorIds = Constellation.Auditors.ToDictionary(a => Constellation.GetAuditorId(a.PubKey), a => a.PubKey);
            AuditorPubKeys = AuditorIds.ToDictionary(a => a.Value, a => a.Key);

            SetRole();

            AccountStorage = new AccountStorage(snapshot.Accounts);

            Exchange?.Dispose(); Exchange = Exchange.RestoreExchange(snapshot.Settings.Assets, snapshot.Orders, IsAlpha, useLegacyOrderbook);

            SetupPaymentProviders(snapshot.Cursors);

            AuditResultManager?.Dispose(); AuditResultManager = new ResultManager(this);

            DisposeAnalyticsManager();

            AnalyticsManager = new AnalyticsManager(
                PermanentStorage,
                DepthsSubscription.Precisions.ToList(),
                Constellation.Assets.Where(a => !a.IsQuoteAsset).Select(a => a.Code).ToList(), //all but base asset
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

        private void SetupPaymentProviders(Dictionary<string, string> cursors)
        {
            PaymentProvidersManager?.Dispose();

            var settings = Constellation.Providers.Select(p =>
            {
                var providerId = PaymentProviderBase.GetProviderId(p.Provider, p.Name);
                cursors.TryGetValue(providerId, out var cursor);
                var settings = p.ToProviderModel(cursor);
                return settings;
            }).ToList();

            PaymentProvidersManager = new PaymentProvidersManager(PaymentProviderFactory, settings, GetAbsolutePath(Settings.PaymentConfigPath));

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
            this.NotifyAuditors(new StateUpdateMessage { State = state }.CreateEnvelope());
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

        public Dictionary<byte, RawPubKey> AuditorIds { get; private set; }

        public Dictionary<RawPubKey, byte> AuditorPubKeys { get; private set; }
    }
}