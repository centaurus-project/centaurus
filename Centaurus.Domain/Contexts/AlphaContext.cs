using Centaurus.DAL;
using Centaurus.Exchange.Analytics;
using Centaurus.Models;
using Centaurus.Stellar;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AlphaContext : ExecutionContext<AlphaContext, AlphaSettings>
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public AlphaContext(AlphaSettings settings, IStorage storage, StellarDataProviderBase stellarDataProvider, bool useLegacyOrderbook = false)
            : base(settings, storage, stellarDataProvider, useLegacyOrderbook)
        {
            AppState = new AlphaStateManager(this);
            AppState.StateChanged += AppState_StateChanged;

            QuantumStorage = new AlphaQuantumStorage();

            QuantumHandler = new AlphaQuantumHandler(this);

            ConnectionManager = new ConnectionManager(this);

            SubscriptionsManager = new SubscriptionsManager();
            InfoConnectionManager = new InfoConnectionManager(this);

            Catchup = new AlphaCatchup(this);

            MessageHandlers = new MessageHandlers<AlphaWebSocketConnection, AlphaContext>(this);

            InfoCommandsHandlers = new InfoCommandsHandlers(this);
        }

        public override bool IsAlpha => true;

        public override StateManager AppState { get; }

        public override QuantumStorageBase QuantumStorage { get; }

        public override QuantumHandler QuantumHandler { get; }

        public override MessageHandlers MessageHandlers { get; }
        public InfoCommandsHandlers InfoCommandsHandlers { get; }
        public AlphaCatchup Catchup { get; }
        public ConnectionManager ConnectionManager { get; }
        public InfoConnectionManager InfoConnectionManager { get; }
        public SubscriptionsManager SubscriptionsManager { get; }
        public AuditLedgerManager AuditLedgerManager { get; private set; }
        public ResultManager AuditResultManager { get; private set; }
        public AnalyticsManager AnalyticsManager { get; private set; }

        public override async Task Setup(Snapshot snapshot)
        {
            if (Exchange != null)
                Exchange.OnUpdates -= Exchange_OnUpdates;
            if (WithdrawalStorage != null)
                WithdrawalStorage.OnSubmitTimer -= OnWithdrawalSubmitTimer;

            await base.Setup(snapshot);

            WithdrawalStorage.OnSubmitTimer += OnWithdrawalSubmitTimer;

            TxListener?.Dispose(); TxListener = new AlphaTxListener(this, snapshot.TxCursor);

            AuditLedgerManager?.Dispose(); AuditLedgerManager = new AuditLedgerManager(this);

            AuditResultManager?.Dispose(); AuditResultManager = new ResultManager(this);

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

            DisposePerformanceStatisticsManager();
            PerformanceStatisticsManager = new AlphaPerformanceStatisticsManager(this);
            PerformanceStatisticsManager.OnUpdates += PerformanceStatisticsManager_OnUpdates;
        }

        public override void Dispose()
        {
            base.Dispose();
            QuantumHandler.Dispose();
            AuditLedgerManager?.Dispose();
            AuditResultManager?.Dispose();
            DisposeAnalyticsManager().Wait();
            DisposePerformanceStatisticsManager();
        }

        protected override void AppState_StateChanged(StateChangedEventArgs stateChangedEventArgs)
        {
            var state = stateChangedEventArgs.State;
            var prevState = stateChangedEventArgs.PrevState;
            if (state != ApplicationState.Ready && prevState == ApplicationState.Ready) //close all connections (except auditors)
                ConnectionManager.CloseAllConnections(false).Wait();

            base.AppState_StateChanged(stateChangedEventArgs);
        }

        private void DisposePerformanceStatisticsManager()
        {
            if (PerformanceStatisticsManager != null)
            {
                PerformanceStatisticsManager.OnUpdates -= PerformanceStatisticsManager_OnUpdates;
                PerformanceStatisticsManager.Dispose();
            }
        }

        private void PerformanceStatisticsManager_OnUpdates(PerformanceStatistics statistics)
        {
            if (!SubscriptionsManager.TryGetSubscription(PerformanceStatisticsSubscription.SubscriptionName, out var subscription))
                return;
            InfoConnectionManager.SendSubscriptionUpdate(subscription, PerformanceStatisticsUpdate.Generate((AlphaPerformanceStatistics)statistics, PerformanceStatisticsSubscription.SubscriptionName));
        }

        async Task OnWithdrawalSubmitTimer(Dictionary<byte[], WithdrawalWrapper> withdrawals)
        {
            try
            {
                if (AppState.State == ApplicationState.Ready)
                    await Cleanup(withdrawals);
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Error on withdrawal cleanup.");
                AppState.State = ApplicationState.Failed;
            }
        }

        private async Task Cleanup(Dictionary<byte[], WithdrawalWrapper> withdrawals)
        {
            byte[][] expiredTransactions = null;
            var currentTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            expiredTransactions = withdrawals.Where(w => w.Value.IsExpired(currentTimeSeconds)).Select(w => w.Key).ToArray();

            if (expiredTransactions.Length < 1)
                return;

            //we must ignore all transactions that was submitted. TxListener will handle submitted transactions.
            var unhandledTxs = await GetUnhandledTx();
            foreach (var expiredTransaction in expiredTransactions.Where(tx => !unhandledTxs.Contains(tx, ByteArrayComparer.Default)))
                _ = QuantumHandler.HandleAsync(new WithrawalsCleanupQuantum { ExpiredWithdrawal = expiredTransaction }.CreateEnvelope());
        }

        private async Task<List<byte[]>> GetUnhandledTx()
        {
            var retries = 1;
            while (true)
            {
                try
                {
                    var limit = 200;
                    var unhandledTxs = new List<byte[]>();
                    var result = await StellarDataProvider.GetTransactions(Constellation.Vault.ToString(), TxCursorManager.TxCursor, limit);
                    while (result.Count > 0)
                    {
                        unhandledTxs.AddRange(result.Select(r => ByteArrayExtensions.FromHexString(r.Hash)));
                        if (result.Count != limit)
                            break;
                        result = await StellarDataProvider.GetTransactions(Constellation.Vault.ToString(), result.Last().PagingToken, limit);
                    }
                    return unhandledTxs;
                }
                catch
                {
                    if (retries == 5)
                        throw;
                    await Task.Delay(retries * 1000);
                    retries++;
                }
            }
        }

        #region Analytics

        private System.Threading.SemaphoreSlim analyticsUpdateSyncRoot = new System.Threading.SemaphoreSlim(1);

        private async void AnalyticsManager_OnUpdate()
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

        private async Task DisposeAnalyticsManager()
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

        private void AnalyticsManager_OnError(Exception exc)
        {
            logger.Error(exc, "Analytics manager error.");
            AppState.State = ApplicationState.Failed;
        }

        private async void Exchange_OnUpdates(ExchangeUpdate updates)
        {
            if (updates == null)
                return;
            if (AnalyticsManager != null)
                await AnalyticsManager.OnUpdates(updates);
        }

        #endregion

    }
}
