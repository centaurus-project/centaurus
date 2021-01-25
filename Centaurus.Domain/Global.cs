﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
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
        /// Setups Global object
        /// </summary>
        /// <param name="settings">Application config</param>
        /// <param name="storage">Permanent storage object</param>
        public static async Task Setup(BaseSettings settings, IStorage storage)
        {
            ExtensionsManager = new ExtensionsManager();

            Settings = settings ?? throw new ArgumentNullException(nameof(settings));

            DynamicSerializersInitializer.Init();

            IsAlpha = Settings is AlphaSettings;

            PermanentStorage = storage;
            await PermanentStorage.OpenConnection(settings.ConnectionString);

            PersistenceManager = new PersistenceManager(PermanentStorage);

            StellarNetwork = new StellarNetwork(Settings.NetworkPassphrase, Settings.HorizonUrl);
            QuantumProcessor = new QuantumProcessorsStorage();

            PendingUpdatesManager = new PendingUpdatesManager();

            AppState = IsAlpha ? new AlphaStateManager() : (StateManager)new AuditorStateManager();
            AppState.StateChanged += AppState_StateChanged;

            //try to load last settings, we need it to know current auditors
            var lastHash = new byte[] { };
            var lastApex = await PersistenceManager.GetLastApex();
            if (lastApex >= 0)
            {
                var lastQuantum = await PersistenceManager.GetQuantum(lastApex);
                lastHash = lastQuantum.Message.ComputeHash();
                logger.Trace($"Last hash is {Convert.ToBase64String(lastHash)}");
                var snapshot = await PersistenceManager.GetSnapshot(lastApex);
                await Setup(snapshot);
                if (IsAlpha)
                    AppState.State = ApplicationState.Rising;//Alpha should ensure that it has all quanta from auditors
                else
                    AppState.State = ApplicationState.Running;
            }
            else
                //if no snapshot, the app is in init state
                AppState.State = ApplicationState.WaitingForInit;

            var lastQuantumApex = lastApex < 0 ? 0 : lastApex;
            QuantumStorage = IsAlpha
                ? (QuantumStorageBase)new AlphaQuantumStorage(lastQuantumApex, lastHash)
                : new AuditorQuantumStorage(lastQuantumApex, lastHash);

            QuantumHandler = new QuantumHandler(QuantumStorage.CurrentApex);
        }

        private static void AppState_StateChanged(StateChangedEventArgs stateChangedEventArgs)
        {
            var state = stateChangedEventArgs.State;
            if (!(PendingUpdatesManager?.IsRunning ?? true) &&
                (state == ApplicationState.Running 
                || state == ApplicationState.Ready
                || state == ApplicationState.WaitingForInit))
            PendingUpdatesManager?.Start();
            if (state != ApplicationState.Ready 
                && stateChangedEventArgs.PrevState == ApplicationState.Running) //close all connections (except auditors) if Alpha is not in Ready state
                ConnectionManager.CloseAllConnections(false).Wait();
        }

        public static async Task TearDown()
        {
            PendingUpdatesManager?.Stop(); PendingUpdatesManager?.Dispose(); 
            AuditLedgerManager?.Dispose();
            AuditResultManager?.Dispose();
            await DisposeAnalyticsManager();

            AuditLedgerManager?.Dispose();
            AuditResultManager?.Dispose(); 
            ExtensionsManager?.Dispose(); 
            WithdrawalStorage?.Dispose();
        }

        public static async Task Setup(Snapshot snapshot)
        {
            Constellation = snapshot.Settings;

            AccountStorage = new AccountStorage(snapshot.Accounts, Constellation.RequestRateLimits);

            Exchange = Exchange.RestoreExchange(snapshot.Settings.Assets, snapshot.Orders, IsAlpha);

            WithdrawalStorage?.Dispose(); WithdrawalStorage = new WithdrawalStorage(snapshot.Withdrawals, (!EnvironmentHelper.IsTest && IsAlpha));

            TxCursorManager = new TxCursorManager(snapshot.TxCursor);

            TxListener?.Dispose(); TxListener = IsAlpha ? (TxListenerBase)new AlphaTxListener(snapshot.TxCursor) : new AuditorTxListener(snapshot.TxCursor);

            if (IsAlpha)
            {
                AuditLedgerManager?.Dispose(); AuditLedgerManager = new AuditLedgerManager();

                AuditResultManager?.Dispose(); AuditResultManager = new AuditResultManager();

                await DisposeAnalyticsManager();

                AnalyticsManager = new AnalyticsManager(
                    PermanentStorage,
                    DepthsSubscription.Precisions.ToList(),
                    Constellation.Assets.Where(a => !a.IsXlm).Select(a => a.Id).ToList(),
                    snapshot.Orders.Select(o => o.ToOrderInfo()).ToList()
                );

                await AnalyticsManager.Restore(DateTime.UtcNow);
                AnalyticsManager.StartTimers();

                AnalyticsManager.OnError += AnalyticsManager_OnError;
                AnalyticsManager.OnUpdate += AnalyticsManager_OnUpdate;
                Exchange.OnUpdates += Exchange_OnUpdates;
            }

            ExtensionsManager?.Dispose(); ExtensionsManager = new ExtensionsManager();
            ExtensionsManager.RegisterAllExtensions();
        }

        public static Exchange Exchange { get; private set; }
        public static PersistenceManager PersistenceManager { get; private set; }

        public static PendingUpdatesManager PendingUpdatesManager { get; set; }

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
        public static QuantumStorageBase QuantumStorage { get; private set; }
        public static AccountStorage AccountStorage { get; private set; }
        public static WithdrawalStorage WithdrawalStorage { get; private set; }
        public static QuantumHandler QuantumHandler { get; private set; }
        public static AuditLedgerManager AuditLedgerManager { get; private set; }
        public static AuditResultManager AuditResultManager { get; private set; }
        public static TxListenerBase TxListener { get; private set; }
        public static TxCursorManager TxCursorManager { get; private set; }
        public static ExtensionsManager ExtensionsManager { get; private set; }
        public static StateManager AppState { get; private set; }
        public static QuantumProcessorsStorage QuantumProcessor { get; private set; }
        public static AnalyticsManager AnalyticsManager { get; private set; }
        public static bool IsAlpha { get; private set; }
        public static IStorage PermanentStorage { get; private set; }
        public static BaseSettings Settings { get; private set; }
        public static StellarNetwork StellarNetwork { get; private set; }
        public static HashSet<int> AssetIds { get; private set; }

        //TODO: move it to separate class
        #region Analytics

        private static System.Threading.SemaphoreSlim analyticsUpdateSyncRoot = new System.Threading.SemaphoreSlim(1);

        private static async void AnalyticsManager_OnUpdate()
        {
            await analyticsUpdateSyncRoot.WaitAsync();
            try
            {
                var updates = new Dictionary<BaseSubscription, SubscriptionUpdateBase>();
                foreach (var subscription in InfoConnectionManager.GetActiveSubscriptions())
                {
                    var update = default(SubscriptionUpdateBase);
                    switch (subscription)
                    {
                        case DepthsSubscription depthsSubscription:
                            {
                                var depth = AnalyticsManager.MarketDepthsManager.GetDepth(depthsSubscription.Market, depthsSubscription.Precision);
                                update = MarketDepthUpdate.Generate(depth, depthsSubscription.Name);
                            }
                            break;
                        case PriceHistorySubscription priceHistorySubscription:
                            {
                                var priceFrames = await AnalyticsManager.PriceHistoryManager.GetPriceHistory(0, priceHistorySubscription.Market, priceHistorySubscription.FramePeriod);
                                update = PriceHistoryUpdate.Generate(priceFrames.frames, priceHistorySubscription.Name);
                            }
                            break;
                        case TradesFeedSubscription tradesFeedSubscription:
                            {
                                var trades = AnalyticsManager.TradesHistoryManager.GetTrades(tradesFeedSubscription.Market);
                                update = TradesFeedUpdate.Generate(trades, tradesFeedSubscription.Name);
                            }
                            break;
                        case AllMarketTickersSubscription allMarketTickersSubscription:
                            {
                                var allTickers = AnalyticsManager.MarketTickersManager.GetAllTickers();
                                update = AllTickersUpdate.Generate(allTickers, allMarketTickersSubscription.Name);
                            }
                            break;
                        case MarketTickerSubscription marketTickerSubscription:
                            {
                                var marketTicker = AnalyticsManager.MarketTickersManager.GetMarketTicker(marketTickerSubscription.Market);
                                update = MarketTickerUpdate.Generate(marketTicker, marketTickerSubscription.Name);
                            }
                            break;
                        default:
                            throw new Exception($"{subscription.Name} subscription is not supported.");
                    }
                    if (update != null)
                        updates.Add(subscription, update);
                }
                InfoConnectionManager.SendSubscriptionUpdates(updates);
            }
            finally
            {
                analyticsUpdateSyncRoot.Release();
            }
        }

        private static async Task DisposeAnalyticsManager()
        {
            if (AnalyticsManager != null)
            {
                try
                {
                    await AnalyticsManager.SaveUpdates(PermanentStorage);
                }
                catch
                {
                    throw new Exception("Unable to save trades history.");
                }
                Exchange.OnUpdates -= Exchange_OnUpdates;
                AnalyticsManager.OnError -= AnalyticsManager_OnError;
                AnalyticsManager.OnUpdate -= AnalyticsManager_OnUpdate;
                AnalyticsManager.Dispose();
            }
        }

        private static void AnalyticsManager_OnError(Exception exc)
        {
            logger.Error(exc, "Analytics manager error.");
            AppState.State = ApplicationState.Failed;
        }

        private static async void Exchange_OnUpdates(ExchangeUpdate updates)
        {
            if (updates == null)
                return;
            await AnalyticsManager.OnUpdates(updates);
        }

        #endregion
    }
}
