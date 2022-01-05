using Centaurus.Client;
using Centaurus.Domain.Models;
using Centaurus.Exchange.Analytics;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PersistentStorage;
using Centaurus.PersistentStorage.Abstraction;
using Microsoft.Extensions.Caching.Memory;
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

            ExtensionsManager = new ExtensionsManager(GetAbsolutePath(Settings.ExtensionsConfigFilePath));

            DataProvider = new DataProvider(this);

            QuantumProcessor = new QuantumProcessorsStorage(this);

            MessageHandlers = new MessageHandlers(this);

            InfoCommandsHandlers = new InfoCommandsHandlers(this);

            IncomingConnectionManager = new IncomingConnectionManager(this);

            SubscriptionsManager = new SubscriptionsManager();

            InfoConnectionManager = new InfoConnectionManager(this);

            ProxyWorker = new ProxyWorker(this);

            Catchup = new Catchup(this);

            var persistentData = DataProvider.GetPersistentData();

            ConstellationSettingsManager = new ConstellationSettingsManager(this, persistentData.snapshot?.ConstellationSettings);

            var currentState = persistentData == default ? State.WaitingForInit : State.Rising;
            NodesManager = new NodesManager(this, currentState);
            NodesManager.CurrentNode.StateChanged += CurrentNode_StateChanged;

            var lastApex = persistentData.snapshot?.Apex ?? 0;
            var lastHash = persistentData.snapshot?.LastHash ?? new byte[32];

            StateNotifier = new StateNotifierWorker(this);

            PendingUpdatesManager = new UpdatesManager(this);

            QuantumHandler = new QuantumHandler(this, lastApex, lastHash);

            ResultManager = new ResultManager(this);

            PerformanceStatisticsManager = new PerformanceStatisticsManager(this);

            SyncStorage = new SyncStorage(this, lastApex);

            SyncQuantaDataWorker = new SyncQuantaDataWorker(this);

            PaymentProvidersManager = new PaymentProvidersManager(PaymentProviderFactory, GetAbsolutePath(Settings.PaymentConfigPath));
            PaymentProvidersManager.OnRegistered += PaymentProvidersManager_OnRegistered;
            PaymentProvidersManager.OnRemoved += PaymentProvidersManager_OnRemoved;

            SetNodes().Wait();

            //apply snapshot if not null
            if (persistentData.snapshot != null)
                Init(persistentData.snapshot);

            HandlePendingQuanta(persistentData.pendingQuanta);
        }

        private void PaymentProvidersManager_OnRemoved(PaymentProviderBase provider)
        {
            provider.OnPaymentCommit -= PaymentProvider_OnPaymentCommit;
            provider.OnError -= PaymentProvider_OnError;
        }

        private void PaymentProvidersManager_OnRegistered(PaymentProviderBase provider)
        {
            provider.OnPaymentCommit += PaymentProvider_OnPaymentCommit;
            provider.OnError += PaymentProvider_OnError;
        }

        private void CurrentNode_StateChanged(StateChangedEventArgs stateChangedEventArgs)
        {
            var state = stateChangedEventArgs.State;
            var prevState = stateChangedEventArgs.PrevState;
            if (state != State.Ready && prevState == State.Ready) //close all connections (except auditors)
                IncomingConnectionManager.CloseAllConnections(false).Wait();
            if (prevState == State.Rising && (state == State.Running || state == State.Ready))
                //after node successfully started, the pending quanta can be deleted
                PersistentStorage.DeletePendingQuanta();
            if (state == State.Failed)
                Complete();
        }

        private void HandlePendingQuanta(List<CatchupQuantaBatchItem> pendingQuanta)
        {
            _ = Catchup.AddNodeBatch(Settings.KeyPair, new CatchupQuantaBatch
            {
                Quanta = pendingQuanta ?? new List<CatchupQuantaBatchItem>(),
                HasMore = false
            });
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
            AccountStorage = new AccountStorage(snapshot.Accounts);

            Exchange = Exchange.RestoreExchange(ConstellationSettingsManager.Current.Assets, snapshot.Orders, Settings.IsPrimeNode());

            PaymentProvidersManager.RegisterProviders(ConstellationSettingsManager.Current, snapshot.Cursors);

            AnalyticsManager = new AnalyticsManager(
                PersistentStorage,
                DepthsSubscription.Precisions.ToList(),
                ConstellationSettingsManager.Current.Assets.Where(a => !a.IsQuoteAsset).Select(a => a.Code).ToList(), //all but quote asset
                snapshot.Orders.Select(o => o.Order.ToOrderInfo()).ToList()
            );

            AnalyticsManager.Restore(DateTime.UtcNow);
            AnalyticsManager.StartTimers();

            AnalyticsManager.OnError += AnalyticsManager_OnError;
            AnalyticsManager.OnUpdate += AnalyticsManager_OnUpdate;
            Exchange.OnUpdates += Exchange_OnUpdates;
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

            OnComplete?.Invoke();
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

            //if init quantum wasn't save, don't save pending quanta
            if (PendingUpdatesManager.LastPersistedApex == 0)
                return;

            //persist all pending quanta
            PendingUpdatesManager.PersistPendingQuanta();
        }

        private async Task SetNodes()
        {
            var auditors = ConstellationSettingsManager.Current != null
                ? ConstellationSettingsManager.Current.Auditors.ToList()
                : Settings.GenesisAuditors.Select(a => new Auditor { PubKey = a.PubKey, Address = a.Address }).ToList();
            await NodesManager.SetNodes(auditors);
        }

        private void PaymentProvider_OnPaymentCommit(PaymentProviderBase paymentProvider, PaymentProvider.Models.DepositNotificationModel notification)
        {
            if (!(NodesManager.IsAlpha && NodesManager.CurrentNode.State == State.Ready))
                throw new OperationCanceledException($"Current server is not ready to process deposits. Is Alpha: {NodesManager.IsAlpha}, State: {NodesManager.CurrentNode.State}");

            QuantumHandler.HandleAsync(new DepositQuantum { Source = notification.ToDomainModel() }, Task.FromResult(true));
        }

        private void PaymentProvider_OnError(PaymentProviderBase paymentProvider, Exception exc)
        {
            logger.Error(exc, $"Error occurred in {paymentProvider.Id}");
        }

        public void Dispose()
        {
            ExtensionsManager?.Dispose();
            PaymentProvidersManager.Dispose();

            SyncQuantaDataWorker.Dispose();

            ResultManager.Dispose();
            DisposeAnalyticsManager();
            PerformanceStatisticsManager.Dispose();
        }

        public event Action OnComplete;

        public DataProvider DataProvider { get; }

        public ResultManager ResultManager { get; }

        public UpdatesManager PendingUpdatesManager { get; }

        public ExtensionsManager ExtensionsManager { get; }

        public QuantumProcessorsStorage QuantumProcessor { get; }

        public SyncStorage SyncStorage { get; }

        public IPersistentStorage PersistentStorage { get; }

        public Settings Settings { get; }

        public PaymentProvidersFactoryBase PaymentProviderFactory { get; }

        internal NodesManager NodesManager { get; }

        internal StateNotifierWorker StateNotifier { get; }

        internal ConstellationSettingsManager ConstellationSettingsManager { get; }

        public QuantumHandler QuantumHandler { get; }

        public IncomingConnectionManager IncomingConnectionManager { get; }

        public SubscriptionsManager SubscriptionsManager { get; }

        public InfoConnectionManager InfoConnectionManager { get; }

        private SyncQuantaDataWorker SyncQuantaDataWorker { get; }

        internal ProxyWorker ProxyWorker { get; }

        internal Catchup Catchup { get; }

        public InfoCommandsHandlers InfoCommandsHandlers { get; }

        public MessageHandlers MessageHandlers { get; }

        public PaymentProvidersManager PaymentProvidersManager { get; }

        public Exchange Exchange { get; private set; }

        public AccountStorage AccountStorage { get; private set; }

        private PerformanceStatisticsManager PerformanceStatisticsManager { get; }

        public AnalyticsManager AnalyticsManager { get; private set; }

        public OutgoingConnectionFactoryBase OutgoingConnectionFactory { get; }
    }
}