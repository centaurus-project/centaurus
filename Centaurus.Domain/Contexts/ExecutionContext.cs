using Centaurus.Client;
using Centaurus.Domain.Models;
using Centaurus.Exchange.Analytics;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PersistentStorage;
using Centaurus.PersistentStorage.Abstraction;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public partial class ExecutionContext : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public ExecutionContext(Settings settings)
            : this(
                  settings,
                  new PersistentStorageAbstraction(),
                  PaymentProvidersFactoryBase.Default,
                  OutgoingConnectionFactoryBase.Default)
        { }

        /// <param name="settings">Application config</param>
        /// <param name="storage">Persistent storage object</param>
        internal ExecutionContext(
            Settings settings, 
            IPersistentStorage storage, 
            PaymentProvidersFactoryBase paymentProviderFactory, 
            OutgoingConnectionFactoryBase connectionFactory)
        {
            DynamicSerializersInitializer.Init();

            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            PersistentStorage = storage ?? throw new ArgumentNullException(nameof(storage));

            OutgoingConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

            PaymentProviderFactory = paymentProviderFactory ?? throw new ArgumentNullException(nameof(paymentProviderFactory));

            PersistentStorage.Connect(GetAbsolutePath(Settings.ConnectionString));

            RoleManager = new RoleManager((CentaurusNodeParticipationLevel)Settings.ParticipationLevel);

            ExtensionsManager = new ExtensionsManager(GetAbsolutePath(Settings.ExtensionsConfigFilePath));

            DataProvider = new DataProvider(this);

            QuantumProcessor = new QuantumProcessorsStorage(this);

            PendingUpdatesManager = new UpdatesManager(this);

            MessageHandlers = new MessageHandlers(this);

            InfoCommandsHandlers = new InfoCommandsHandlers(this);

            IncomingConnectionManager = new IncomingConnectionManager(this);

            SubscriptionsManager = new SubscriptionsManager();

            InfoConnectionManager = new InfoConnectionManager(this);

            SyncQuantaDataWorker = new SyncQuantaDataWorker(this);

            ProxyWorker = new ProxyWorker(this);

            Catchup = new Catchup(this);

            var persistentData = DataProvider.GetPersistentData();

            StateNotifier = new StateNotifierWorker(this);
            var currentState = persistentData == default ? State.WaitingForInit : State.Rising;
            NodesManager = new NodesManager(this, currentState);

            var lastApex = persistentData.snapshot?.Apex ?? 0;
            var lastHash = persistentData.snapshot?.LastHash ?? new byte[32];

            SyncStorage = new SyncStorage(this, lastApex);

            QuantumHandler = new QuantumHandler(this, lastApex, lastHash);

            ResultManager = new ResultManager(this);

            //apply data, if presented in db
            if (persistentData != default)
            {
                //apply snapshot if not null
                if (persistentData.snapshot != null)
                    Init(persistentData.snapshot);

                if (persistentData.pendingQuanta != null)
                    HandlePendingQuanta(persistentData.pendingQuanta);

                if (!IsAlpha)
                    NodesManager.CurrentNode.Rised();
            }

            if (Constellation == null)
                SetNodes();
        }

        private void HandlePendingQuanta(List<CatchupQuantaBatchItem> pendingQuanta)
        {
            _ = Catchup.AddAuditorState(Settings.KeyPair, new CatchupQuantaBatch { Quanta = pendingQuanta, HasMore = false });
        }

        private string GetAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;
            return Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(Settings.CWD, path.Trim('.').Trim('\\').Trim('/')));
        }

        public void Init(Snapshot snapshot)
        {
            UpdateConstellationSettings(snapshot.ConstellationSettings);

            AccountStorage = new AccountStorage(snapshot.Accounts);

            Exchange = Exchange.RestoreExchange(Constellation.Assets, snapshot.Orders, RoleManager.ParticipationLevel == CentaurusNodeParticipationLevel.Prime);

            SetupPaymentProviders(snapshot.Cursors);

            AnalyticsManager = new AnalyticsManager(
                PersistentStorage,
                DepthsSubscription.Precisions.ToList(),
                Constellation.Assets.Where(a => !a.IsQuoteAsset).Select(a => a.Code).ToList(), //all but quote asset
                snapshot.Orders.Select(o => o.Order.ToOrderInfo()).ToList()
            );

            AnalyticsManager.Restore(DateTime.UtcNow);
            AnalyticsManager.StartTimers();

            AnalyticsManager.OnError += AnalyticsManager_OnError;
            AnalyticsManager.OnUpdate += AnalyticsManager_OnUpdate;
            Exchange.OnUpdates += Exchange_OnUpdates;
        }

        public void UpdateConstellationSettings(ConstellationSettings constellationSettings)
        {
            Constellation = constellationSettings;

            AuditorIds = Constellation.Auditors.ToDictionary(a => Constellation.GetAuditorId(a.PubKey), a => a.PubKey);
            AuditorPubKeys = AuditorIds.ToDictionary(a => a.Value, a => a.Key);
            AlphaId = AuditorPubKeys[Constellation.Alpha];

            //update current auditors
            SetNodes();

            SetRole();

            IncomingConnectionManager.CleanupAuditorConnections();
        }

        public void Complete()
        {
            NodesManager.CurrentNode.Stopped();

            //close all connections
            Task.WaitAll(
                IncomingConnectionManager.CloseAllConnections(),
                InfoConnectionManager.CloseAllConnections(),
                Task.Factory.StartNew(() => NodesManager.ClearNodes())
            );

            PersistPendingQuanta();
        }

        private void PersistPendingQuanta()
        {
            ResultManager.CompleteAdding();

            while (!ResultManager.IsCompleted)
                Thread.Sleep(50);

            //complete current updates container
            PendingUpdatesManager.UpdateBatch(true);
            //save all completed quanta
            PendingUpdatesManager.ApplyUpdates();
            //persist all pending quanta
            PendingUpdatesManager.PersistPendingQuanta();
        }

        private void SetNodes()
        {
            var auditors = Constellation != null
                ? Constellation.Auditors.ToList()
                : Settings.GenesisAuditors.Select(a => new Auditor { PubKey = a.PubKey, Address = a.Address }).ToList();
            NodesManager.SetAuditors(auditors);
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
            {
                RoleManager.SetRole(CentaurusNodeRole.Alpha);
            }
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
                paymentProvider.OnError += PaymentProvider_OnError;
            }
        }

        private void PaymentProvider_OnPaymentCommit(PaymentProviderBase paymentProvider, PaymentProvider.Models.DepositNotificationModel notification)
        {
            if (!IsAlpha || NodesManager.CurrentNode.State != State.Ready)
                throw new OperationCanceledException($"Current server is not ready to process deposits. Is Alpha: {IsAlpha}, State: {NodesManager.CurrentNode.State}");

            QuantumHandler.HandleAsync(new DepositQuantum { Source = notification.ToDomainModel() }, Task.FromResult(true));
        }

        private void PaymentProvider_OnError(PaymentProviderBase paymentProvider, Exception exc)
        {
            logger.Error(exc, $"Error occurred in {paymentProvider.Id}");
        }

        public void Dispose()
        {
            ExtensionsManager?.Dispose();
            PaymentProvidersManager?.Dispose();

            ResultManager.Dispose();
            DisposeAnalyticsManager();
            PerformanceStatisticsManager.Dispose();
        }

        private async void AppState_StateChanged(StateChangedEventArgs stateChangedEventArgs)
        {
            var state = stateChangedEventArgs.State;
            var prevState = stateChangedEventArgs.PrevState;
            if (state != State.Ready && prevState == State.Ready) //close all connections (except auditors)
                await IncomingConnectionManager.CloseAllConnections(false);
            if (prevState == State.Rising && state == State.Running || state == State.Ready)
                //after node successfully started, the pending quanta can be deleted
                PersistentStorage.DeletePendingQuanta();
        }

        public DataProvider DataProvider { get; }

        public ResultManager ResultManager { get; }

        public UpdatesManager PendingUpdatesManager { get; }

        public bool IsAlpha => RoleManager.Role == CentaurusNodeRole.Alpha;

        public ExtensionsManager ExtensionsManager { get; }

        public QuantumProcessorsStorage QuantumProcessor { get; }

        public SyncStorage SyncStorage { get; }

        public RoleManager RoleManager { get; }

        public IPersistentStorage PersistentStorage { get; }

        public Settings Settings { get; }

        public PaymentProvidersFactoryBase PaymentProviderFactory { get; }

        internal NodesManager NodesManager { get; }

        internal StateNotifierWorker StateNotifier { get; }

        public QuantumHandler QuantumHandler { get; }

        public IncomingConnectionManager IncomingConnectionManager { get; }

        public SubscriptionsManager SubscriptionsManager { get; }

        public InfoConnectionManager InfoConnectionManager { get; }

        private SyncQuantaDataWorker SyncQuantaDataWorker { get; }

        internal ProxyWorker ProxyWorker { get; }

        internal Catchup Catchup { get; }

        public InfoCommandsHandlers InfoCommandsHandlers { get; }

        public MessageHandlers MessageHandlers { get; }

        public PaymentProvidersManager PaymentProvidersManager { get; private set; }

        public Exchange Exchange { get; private set; }

        public AccountStorage AccountStorage { get; private set; }

        private PerformanceStatisticsManager PerformanceStatisticsManager { get; }

        public HashSet<int> AssetIds { get; private set; }

        public AnalyticsManager AnalyticsManager { get; private set; }

        public ConstellationSettings Constellation { get; private set; }

        public Dictionary<byte, RawPubKey> AuditorIds { get; private set; }

        public Dictionary<RawPubKey, byte> AuditorPubKeys { get; private set; }

        public OutgoingConnectionFactoryBase OutgoingConnectionFactory { get; }

        public byte AlphaId { get; private set; }

        public event Action OnComplete;
    }
}