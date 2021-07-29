using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public partial class ExecutionContext : IDisposable
    {
        private object syncRoot = new { };

        private void AnalyticsManager_OnUpdate()
        {
            lock(syncRoot)
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
                                var priceFrames = AnalyticsManager.PriceHistoryManager.GetPriceHistory(0, priceHistorySubscription.Market, priceHistorySubscription.FramePeriod);
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
        }

        private void DisposeAnalyticsManager()
        {
            if (AnalyticsManager != null)
            {
                try
                {
                    AnalyticsManager.SaveUpdates(PermanentStorage);
                }
                catch
                {
                    throw new Exception("Unable to save trades history.");
                }
                AnalyticsManager.OnError -= AnalyticsManager_OnError;
                AnalyticsManager.OnUpdate -= AnalyticsManager_OnUpdate;
                AnalyticsManager.Dispose();
            }
        }

        private void AnalyticsManager_OnError(Exception exc)
        {
            AppState.SetState(State.Failed, new Exception("Analytics manager error.", exc));
        }

        private void Exchange_OnUpdates(ExchangeUpdate updates)
        {
            if (updates == null)
                return;
            AnalyticsManager?.OnUpdates(updates);
        }

    }
}
