using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Centaurus.Analytics;
using Centaurus.DAL;
using Centaurus.DAL.Mongo;
using Centaurus.Exchange.Analytics;
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
        public static void Init(BaseSettings settings, IStorage storage)
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

            Exchange = Exchange.RestoreExchange(snapshot.Settings.Assets, snapshot.Orders, IsAlpha);

            AuditLedgerManager?.Dispose(); AuditLedgerManager = new AuditLedgerManager();

            AuditResultManager?.Dispose(); AuditResultManager = new AuditResultManager();

            WithdrawalStorage?.Dispose(); WithdrawalStorage = new WithdrawalStorage(snapshot.Withdrawals, (!EnvironmentHelper.IsTest && IsAlpha));

            TxManager?.Dispose(); TxManager = new TxManager(snapshot.TxCursor);

            if (IsAlpha)
            {
                if (AnalyticsManager != null)
                {
                    try
                    {
                        AnalyticsManager.SaveUpdates(PermanentStorage).Wait();
                    }
                    catch
                    {
                        throw new Exception("Unable to save trades history.");
                    }
                    Exchange.OnUpdates -= Exchange_OnTrade;
                    AnalyticsManager.OnError -= AnalyticsManager_OnError;
                    AnalyticsManager.OnUpdate -= AnalyticsManager_OnUpdate;
                    AnalyticsManager.Dispose();
                }
                AnalyticsManager = new AnalyticsManager(PermanentStorage, DepthsSubscription.Precisions.ToList(), Exchange.OrderMap, Constellation.Assets.Where(a => !a.IsXlm).Select(a => a.Id).ToList());
                AnalyticsManager.Restore(DateTime.UtcNow).Wait();
                AnalyticsManager.OnError += AnalyticsManager_OnError;
                AnalyticsManager.OnUpdate += AnalyticsManager_OnUpdate;
                Exchange.OnUpdates += Exchange_OnTrade;
            }

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
        public static TxManager TxManager { get; private set; }
        public static ExtensionsManager ExtensionsManager { get; private set; }
        public static StateManager AppState { get; private set; }
        public static QuantumProcessorsStorage QuantumProcessor { get; private set; }
        public static AnalyticsManager AnalyticsManager { get; private set; }
        public static bool IsAlpha { get; private set; }
        public static IStorage PermanentStorage { get; private set; }
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

        #region Analytics

        private static async void AnalyticsManager_OnUpdate()
        {
            var updates = new Dictionary<BaseSubscription, SubscriptionUpdateBase>();
            foreach (var subs in InfoConnectionManager.GetActiveSubscriptions())
            {
                switch (subs)
                {
                    case DepthsSubscription depthsSubscription:
                        {
                            var depth = AnalyticsManager.MarketDepthsManager.GetDepth(depthsSubscription.Market, depthsSubscription.Precision);

                            if (depth != null)
                                updates.Add(depthsSubscription, new MarketDepthUpdate { MarketDepth = depth, UpdateDate = depth.UpdatedAt });
                        }
                        break;
                    case PriceHistorySubscription priceHistorySubscription:
                        {
                            var frames = await AnalyticsManager.OHLCManager.GetFrames(0, priceHistorySubscription.Market, priceHistorySubscription.FramePeriod);
                            if (frames.frames.Count > 0)
                                updates.Add(priceHistorySubscription, new PriceHistoryUpdate { Prices = frames.frames, UpdateDate = frames.frames.Max(f => f.UpdatedAt) });
                        }
                        break;
                    case TradesFeedSubscription tradesFeedSubscription:
                        {
                            var trades = AnalyticsManager.TradesHistoryManager.GetTrades(tradesFeedSubscription.Market);
                            updates.Add(tradesFeedSubscription, new TradesFeedUpdate { Trades = trades, UpdateDate = new DateTime(trades.FirstOrDefault()?.Timestamp ?? 0, DateTimeKind.Utc) });
                        }
                        break;
                    case AllMarketTickersSubscription allMarketTickersSubscription:
                        {
                            var allTickers = AnalyticsManager.MarketTickersManager.GetAllTickers();
                            if (allTickers.Count > 0)
                                updates.Add(allMarketTickersSubscription, new AllTickersUpdate { Tickers = allTickers, UpdateDate = allTickers.Max(t => t.UpdatedAt) });
                        }
                        break;
                    case MarketTickerSubscription marketTickerSubscription:
                        {
                            var marketTicker = AnalyticsManager.MarketTickersManager.GetMarketTicker(marketTickerSubscription.Market);
                            if (marketTicker != null)
                            updates.Add(marketTickerSubscription, new MarketTickerUpdate { MarketTicker = marketTicker, UpdateDate = marketTicker.UpdatedAt });
                        }
                        break;
                    default:
                        break;
                }
            }
            InfoConnectionManager.SendSubscriptionUpdates(updates);
        }

        private static void AnalyticsManager_OnError(Exception exc)
        {
            logger.Error(exc);
            AppState.State = ApplicationState.Failed;
        }

        private static async void Exchange_OnTrade(ExchangeUpdate updates)
        {
            if (updates == null)
                return;
            await AnalyticsManager.OnUpdates(updates);
        }

        #endregion
    }
}
