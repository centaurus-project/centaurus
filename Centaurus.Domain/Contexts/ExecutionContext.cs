using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Centaurus.DAL;
using Centaurus.DAL.Mongo;
using Centaurus.Exchange.Analytics;
using Centaurus.Models;
using Centaurus.Stellar;
using Centaurus.Xdr;
using NLog;

namespace Centaurus.Domain
{
    public abstract class ExecutionContext: IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        /// <param name="settings">Application config</param>
        /// <param name="storage">Permanent storage object</param>
        /// <param name="useLegacyOrderbook"></param>
        public ExecutionContext(BaseSettings settings, IStorage storage, StellarDataProviderBase stellarDataProvider, bool useLegacyOrderbook = false)
        {
            PermanentStorage = storage ?? throw new ArgumentNullException(nameof(storage));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            StellarDataProvider = stellarDataProvider ?? throw new ArgumentNullException(nameof(settings));

            ExtensionsManager = new ExtensionsManager(settings.ExtensionsConfigFilePath);

            PersistenceManager = new PersistenceManager(PermanentStorage);
            QuantumProcessor = new QuantumProcessorsStorage();

            PendingUpdatesManager = new PendingUpdatesManager(this);
            PendingUpdatesManager.OnBatchSaved += PendingUpdatesManager_OnBatchSaved;

            QuantumStorage = new QuantumStorage();

            this.useLegacyOrderbook = useLegacyOrderbook;
        }

        /// <summary>
        /// Delay in seconds
        /// </summary>
        public const int MaxTxSubmitDelay = 5 * 60; //5 minutes

        readonly bool useLegacyOrderbook;

        public virtual async Task Init()
        {
            DynamicSerializersInitializer.Init();

            await PermanentStorage.OpenConnection(Settings.ConnectionString);

            //try to load last settings, we need it to know current auditors
            var lastHash = new byte[] { };
            var lastApex = await PersistenceManager.GetLastApex();
            if (lastApex >= 0)
            {
                var lastQuantum = await PersistenceManager.GetQuantum(lastApex);

                lastHash = lastQuantum.Message.ComputeHash();
                var snapshot = await PersistenceManager.GetSnapshot(lastApex);
                await Setup(snapshot);
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

        public virtual Task Setup(Snapshot snapshot)
        {
            Constellation = snapshot.Settings;

            AccountStorage = new AccountStorage(snapshot.Accounts);

            Exchange?.Dispose(); Exchange = Exchange.RestoreExchange(snapshot.Settings.Assets, snapshot.Orders, IsAlpha, useLegacyOrderbook);

            WithdrawalStorage?.Dispose(); WithdrawalStorage = new WithdrawalStorage(snapshot.Withdrawals);

            TxCursorManager = new TxCursorManager(snapshot.TxCursor);

            return Task.CompletedTask;
        }

        public virtual void Dispose()
        {
            PendingUpdatesManager?.Stop(TimeSpan.FromMilliseconds(0)); PendingUpdatesManager?.Dispose();

            ExtensionsManager?.Dispose();
            WithdrawalStorage?.Dispose();
            TxListener?.Dispose();
        }

        protected virtual void AppState_StateChanged(StateChangedEventArgs stateChangedEventArgs)
        {
            var state = stateChangedEventArgs.State;
            if (!(PendingUpdatesManager?.IsRunning ?? true) &&
                (state == ApplicationState.Running
                || state == ApplicationState.Ready
                || state == ApplicationState.WaitingForInit))
                PendingUpdatesManager?.Start();
        }

        private void PendingUpdatesManager_OnBatchSaved(BatchSavedInfo batchInfo)
        {
            var message = $"Batch saved on the {batchInfo.Retries} try. Quanta count: {batchInfo.QuantaCount}; effects count: {batchInfo.EffectsCount}.";
            if (batchInfo.Retries > 1)
                logger.Warn(message);
            else
                logger.Trace(message);
            PerformanceStatisticsManager?.OnBatchSaved(batchInfo);
        }


        public PersistenceManager PersistenceManager { get; }

        public PendingUpdatesManager PendingUpdatesManager { get; }

        private ConstellationSettings constellation;
        public ConstellationSettings Constellation
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

        public Exchange Exchange { get; protected set; }

        public AccountStorage AccountStorage { get; protected set; }

        public WithdrawalStorage WithdrawalStorage { get; protected set; }

        public TxListenerBase TxListener { get; protected set; }

        public TxCursorManager TxCursorManager { get; protected set; }

        public PerformanceStatisticsManager PerformanceStatisticsManager { get; protected set; }

        public HashSet<int> AssetIds { get; protected set; }

        public ExtensionsManager ExtensionsManager { get; }

        public QuantumProcessorsStorage QuantumProcessor { get; }

        public QuantumStorage QuantumStorage { get; }

        public IStorage PermanentStorage { get; }

        public BaseSettings Settings { get; }

        public StellarDataProviderBase StellarDataProvider { get; }

        public virtual bool IsAlpha { get; } = false;

        public abstract StateManager AppState { get; }

        public virtual QuantumHandler QuantumHandler { get; }

        public virtual MessageHandlers MessageHandlers { get; }
    }

    public abstract class ExecutionContext<TContext, TSettings> : ExecutionContext
        where TContext: ExecutionContext
        where TSettings: BaseSettings
    {
        public ExecutionContext(TSettings settings, IStorage storage, StellarDataProviderBase stellarDataProvider, bool useLegacyOrderbook = false)
            :base(settings, storage, stellarDataProvider, useLegacyOrderbook)
        {
        }

        public new TSettings Settings => (TSettings)base.Settings;
    }
}