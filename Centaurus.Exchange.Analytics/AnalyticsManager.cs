using Centaurus.Analytics;
using Centaurus.DAL;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Centaurus.Exchange.Analytics
{
    public class AnalyticsManager : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        private System.Timers.Timer updatesTimer;

        public AnalyticsManager(IAnalyticsStorage analyticsStorage, List<double> precisions, IOrderMap orders, List<int> markets, int tradesHistorySize = 100)
        {
            this.analyticsStorage = analyticsStorage ?? throw new ArgumentNullException(nameof(analyticsStorage));
            OHLCManager = new OHLCManager(analyticsStorage, markets ?? throw new ArgumentNullException(nameof(markets)));
            TradesHistoryManager = new TradesHistoryManager(markets, tradesHistorySize);
            MarketDepthsManager = new MarketDepthsManager(markets, precisions, orders);
            InitTimer();
        }

        public async Task Restore(DateTime dateTime)
        {
            await OHLCManager.Restore(dateTime);
            MarketDepthsManager.Restore();
        }

        public async Task SaveUpdates(IAnalyticsStorage analyticsStorage, int numberOfTries = 5)
        {
            await syncRoot.WaitAsync();
            try
            {
                var frames = OHLCManager.PullUpdates();
                //var trades = TradesHistoryManager.PullUpdates();
                if (frames.Count < 1)
                    return;
                var currentTry = 0;
                while (currentTry < numberOfTries)
                {
                    try
                    {
                        var frameModels = frames.Select(f => f.ToFrameModel()).ToList();
                        await analyticsStorage.SaveAnalytics(frameModels);
                        break;
                    }
                    catch
                    {
                        currentTry++;
                        if (currentTry == numberOfTries)
                            throw;
                        Thread.Sleep(currentTry * 500);
                    }
                }
            }
            finally
            {
                syncRoot.Release();
            }
        }


        public OHLCManager OHLCManager { get; private set; }
        public TradesHistoryManager TradesHistoryManager { get; private set; }
        public MarketDepthsManager MarketDepthsManager { get; private set; }

        public void Dispose()
        {
            OHLCManager?.Dispose();
            OHLCManager = null;
            TradesHistoryManager = null;
        }

        private void InitTimer()
        {
            updatesTimer = new System.Timers.Timer();
            updatesTimer.Interval = 5 * 1000;
            updatesTimer.AutoReset = false;
            updatesTimer.Elapsed += UpdatesTimer_Elapsed;
            updatesTimer.Start();
        }

        private SemaphoreSlim syncRoot = new SemaphoreSlim(1);

        private async void UpdatesTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                await SaveUpdates(analyticsStorage);
                updatesTimer?.Start();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                //TODO: should we crash Alpha if we are unable to save updates?
            }
        }

        public void OnUpdates(ExchangeUpdate updates)
        {
            OHLCManager.OnTrade(updates.Trades);
            TradesHistoryManager.OnTrade(updates.Market, updates.Trades);
            MarketDepthsManager.OnOrderUpdates(updates.Market, updates.OrderUpdates);
        }

        private IAnalyticsStorage analyticsStorage;
    }
}
