using Centaurus.Analytics;
using Centaurus.DAL;
using NLog;
using System;
using System.Collections.Generic;
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

        public AnalyticsManager(IAnalyticsStorage analyticsStorage, List<int> markets, int tradesHistorySize = 100)
        {
            this.analyticsStorage = analyticsStorage;
            OHLCManager = new OHLCManager(analyticsStorage, markets);
            TradesHistoryManager = new TradesHistoryManager(markets, tradesHistorySize);
            InitTimer();
        }

        public async Task SaveUpdates(IAnalyticsStorage analyticsStorage, int numberOfTries = 5)
        {
            await syncRoot.WaitAsync();
            try
            {
                var frames = OHLCManager.PullUpdates();
                var trades = TradesHistoryManager.PullUpdates();
                var currentTry = 0;
                while (currentTry < numberOfTries)
                {
                    try
                    {
                        await analyticsStorage.SaveAnalytics(
                            frames.Select(f => f.ToFrameModel()).ToList()
                        );
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

        public (int market, Dictionary<OHLCFramePeriod, List<OHLCFrame>> frames, List<Trade> trades) OnTrade(List<Trade> trades)
        {
            if (OHLCManager == null)
                throw new ObjectDisposedException(nameof(AnalyticsManager));

            return (
                market: trades.First().Asset,
                frames: OHLCManager.OnTrade(trades),
                trades: TradesHistoryManager.OnTrade(trades)
            );
        }

        private IAnalyticsStorage analyticsStorage;
    }
}
