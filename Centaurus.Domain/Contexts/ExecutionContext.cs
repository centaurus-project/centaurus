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

        /// <param name="settings">Application config</param>
        /// <param name="storage">Persistent storage object</param>
        public ExecutionContext(Settings settings, IPersistentStorage storage, PaymentProvidersFactoryBase paymentProviderFactory, OutgoingConnectionFactoryBase connectionFactory)
        {

            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            PersistentStorage = storage ?? throw new ArgumentNullException(nameof(storage));
            PersistentStorage.Connect(GetAbsolutePath(Settings.ConnectionString));

            PaymentProviderFactory = paymentProviderFactory ?? throw new ArgumentNullException(nameof(paymentProviderFactory));

            RoleManager = new RoleManager((CentaurusNodeParticipationLevel)Settings.ParticipationLevel);

            ExtensionsManager = new ExtensionsManager(GetAbsolutePath(Settings.ExtensionsConfigFilePath));

            DataProvider = new DataProvider(this);

            QuantumProcessor = new QuantumProcessorsStorage(this);

            PendingUpdatesManager = new UpdatesManager(this);
            PendingUpdatesManager.OnBatchSaved += PendingUpdatesManager_OnBatchSaved;

            MessageHandlers = new MessageHandlers(this);

            InfoCommandsHandlers = new InfoCommandsHandlers(this);

            IncomingConnectionManager = new IncomingConnectionManager(this);

            OutgoingConnectionManager = new OutgoingConnectionManager(this, connectionFactory);

            SubscriptionsManager = new SubscriptionsManager();

            InfoConnectionManager = new InfoConnectionManager(this);

            Catchup = new Catchup(this);

            StateManager = new StateManager(this);
            StateManager.StateChanged += AppState_StateChanged;

            DynamicSerializersInitializer.Init();

            var persistentData = DataProvider.GetPersistentData();

            var lastApex = persistentData.snapshot?.Apex ?? 0;
            var lastHash = persistentData.snapshot?.LastHash ?? new byte[32];

            QuantumStorage = new QuantumStorage(this, lastApex, lastHash);

            ResultManager = new ResultManager(this);

            QuantumHandler = new QuantumHandler(this, lastApex);

            while (!Debugger.IsAttached)
                Thread.Sleep(10000);

            //apply data, if presented in db
            if (persistentData != default)
            {
                StateManager.Init(State.Rising);
                //apply snapshot if not null
                if (persistentData.snapshot != null)
                    Setup(persistentData.snapshot);

                if (persistentData.pendingQuanta != null)
                    HandlePendingQuanta(persistentData.pendingQuanta);


                if (!IsAlpha)
                    StateManager.Rised();
            }

            if (Constellation == null)
            {
                SetAuditorStates();
                //establish connection with genesis auditors
                EstablishOutgoingConnections();
            }
        }

        private void HandlePendingQuanta(List<PendingQuantum> pendingQuanta)
        {
            foreach (var quantum in pendingQuanta)
            {
                try
                {
                    //cache current payload hash
                    var persistentQuantumHash = quantum.Quantum.GetPayloadHash();

                    //handle quantum
                    var quantumProcessingItem = QuantumHandler.HandleAsync(quantum.Quantum, QuantumSignatureValidator.Validate(quantum.Quantum));

                    quantumProcessingItem.OnAcknowledged.Wait();

                    //verify that the pending quantum has current node signature
                    var currentNodeSignature = quantum.Signatures.FirstOrDefault(s => s.AuditorId == Constellation.GetAuditorId(Settings.KeyPair)) ?? throw new Exception($"Unable to get signature for quantum {quantum.Quantum.Apex}");

                    //verify the payload signature
                    if (!Settings.KeyPair.Verify(quantum.Quantum.GetPayloadHash(), currentNodeSignature.PayloadSignature.Data))
                        throw new Exception($"Signature for the quantum {quantum.Quantum.Apex} is invalid.");

                    //validate that quantum payload data is the same
                    if (!persistentQuantumHash.AsSpan().SequenceEqual(quantum.Quantum.GetPayloadHash()))
                        throw new Exception($"Payload hash for the quantum {quantum.Quantum.Apex} is not equal to persistent.");

                    //add signatures
                    ResultManager.Add(new QuantumSignatures { Apex = quantum.Quantum.Apex, Signatures = quantum.Signatures });
                }
                catch (AggregateException exc)
                {
                    //unwrap aggregate exc
                    throw exc.GetBaseException();
                }
            }
        }

        private string GetAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;
            return Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(Settings.CWD, path.Trim('.').Trim('\\').Trim('/')));
        }

        public void Setup(Snapshot snapshot)
        {

            if (Exchange != null)
                Exchange.OnUpdates -= Exchange_OnUpdates;

            Constellation = snapshot.Settings;

            AuditorIds = Constellation.Auditors.ToDictionary(a => Constellation.GetAuditorId(a.PubKey), a => a.PubKey);
            AuditorPubKeys = AuditorIds.ToDictionary(a => a.Value, a => a.Key);
            AlphaId = AuditorPubKeys[Constellation.Alpha];

            //update current auditors
            SetAuditorStates();

            SetRole();

            AccountStorage = new AccountStorage(snapshot.Accounts);

            Exchange?.Dispose(); Exchange = Exchange.RestoreExchange(snapshot.Settings.Assets, snapshot.Orders, RoleManager.ParticipationLevel == CentaurusNodeParticipationLevel.Prime);

            SetupPaymentProviders(snapshot.Cursors);

            DisposeAnalyticsManager();

            AnalyticsManager = new AnalyticsManager(
                PersistentStorage,
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

        public void Complete()
        {
            StateManager.Stopped();

            //close all connections
            Task.WaitAll(
                IncomingConnectionManager.CloseAllConnections(),
                InfoConnectionManager.CloseAllConnections(),
                Task.Factory.StartNew(OutgoingConnectionManager.CloseAllConnections)
            );

            PersistPendingQuanta();
        }

        private void PersistPendingQuanta()
        {
            ResultManager.CompleteAdding();

            while (!ResultManager.IsAddingCompleted)
                Thread.Sleep(50);

            //sleep for a second to make sure that all results were added
            Thread.Sleep(1000);

            //complete current updates container
            PendingUpdatesManager.UpdateBatch(true);
            //save all completed quanta
            PendingUpdatesManager.ApplyUpdates();
            //persist all pending quanta
            PendingUpdatesManager.PersistPendingQuanta();
        }

        private void EstablishOutgoingConnections()
        {
            var auditors = Constellation != null
                ? Constellation.Auditors.Select(a => new Settings.Auditor(a.PubKey, a.Address)).ToList()
                : Settings.GenesisAuditors.ToList();
            OutgoingConnectionManager.Connect(auditors);
        }


        private void SetAuditorStates()
        {
            var auditors = Constellation != null
                ? Constellation.Auditors.Select(a => a.PubKey).ToList()
                : Settings.GenesisAuditors.Select(a => (RawPubKey)a.PubKey).ToList();
            StateManager.SetAuditors(auditors);
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
            if (!IsAlpha || StateManager.State != State.Ready)
                throw new OperationCanceledException($"Current server is not ready to process deposits. Is Alpha: {IsAlpha}, State: {StateManager.State}");

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

            QuantumHandler.Dispose();
            ResultManager.Dispose();
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

        public DataProvider DataProvider { get; }

        public ResultManager ResultManager { get; }

        public UpdatesManager PendingUpdatesManager { get; }

        public bool IsAlpha => RoleManager.Role == CentaurusNodeRole.Alpha;

        public ExtensionsManager ExtensionsManager { get; }

        public QuantumProcessorsStorage QuantumProcessor { get; }

        public QuantumStorage QuantumStorage { get; }

        public RoleManager RoleManager { get; }

        public IPersistentStorage PersistentStorage { get; }

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

        public AnalyticsManager AnalyticsManager { get; private set; }

        public ConstellationSettings Constellation { get; private set; }

        public Dictionary<byte, RawPubKey> AuditorIds { get; private set; }

        public Dictionary<RawPubKey, byte> AuditorPubKeys { get; private set; }

        public byte AlphaId { get; private set; }
    }
}