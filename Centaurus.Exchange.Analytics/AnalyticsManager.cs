using Centaurus.Models;
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

        private System.Timers.Timer saveTimer;
        private System.Timers.Timer updateTimer;

        public AnalyticsManager(IAnalyticsStorage analyticsStorage, List<double> precisions, IOrderMap orders, List<int> markets, int tradesHistorySize = 100)
        {
            this.analyticsStorage = analyticsStorage ?? throw new ArgumentNullException(nameof(analyticsStorage));
            PriceHistoryManager = new PriceHistoryManager(analyticsStorage, markets ?? throw new ArgumentNullException(nameof(markets)));
            TradesHistoryManager = new TradesHistoryManager(markets, tradesHistorySize);
            MarketDepthsManager = new MarketDepthsManager(markets, precisions, orders);
            MarketTickersManager = new MarketTickersManager(markets, PriceHistoryManager);
            InitTimer();
        }

        public async Task Restore(DateTime dateTime)
        {
            await PriceHistoryManager.Restore(dateTime);
            MarketDepthsManager.Restore(dateTime);
        }

        public async Task SaveUpdates(IAnalyticsStorage analyticsStorage, int numberOfTries = 5)
        {
            await syncRoot.WaitAsync();
            try
            {
                var frames = PriceHistoryManager.PullUpdates();
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

        public event Action OnUpdate;

        public event Action<Exception> OnError;

        public PriceHistoryManager PriceHistoryManager { get; private set; }
        public TradesHistoryManager TradesHistoryManager { get; private set; }
        public MarketDepthsManager MarketDepthsManager { get; private set; }
        public MarketTickersManager MarketTickersManager { get; private set; }

        public void Dispose()
        {
            saveTimer?.Stop();
            saveTimer?.Dispose();
            saveTimer = null;

            updateTimer?.Stop();
            updateTimer?.Dispose();
            updateTimer = null;

            PriceHistoryManager?.Dispose();
            PriceHistoryManager = null;
            TradesHistoryManager = null;
        }

        private void InitTimer()
        {
            saveTimer = new System.Timers.Timer();
            saveTimer.Interval = 5 * 1000;
            saveTimer.AutoReset = false;
            saveTimer.Elapsed += SaveTimer_Elapsed;
            saveTimer.Start();

            updateTimer = new System.Timers.Timer();
            updateTimer.Interval = 1000;
            updateTimer.AutoReset = false;
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
        }



        private async void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                PriceHistoryManager.Update();
                await MarketTickersManager.Update();
                updateTimer?.Start();
                OnUpdate?.Invoke();
            }
            catch(Exception exc)
            {
                OnError?.Invoke(exc);
            }
        }

        private SemaphoreSlim syncRoot = new SemaphoreSlim(1);

        private async void SaveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                await SaveUpdates(analyticsStorage);
                saveTimer?.Start();
            }
            catch (Exception exc)
            {
                OnError?.Invoke(exc);
            }
        }

        public async Task OnUpdates(ExchangeUpdate updates)
        {
            await PriceHistoryManager.OnTrade(updates);
            TradesHistoryManager.OnTrade(updates);
            MarketDepthsManager.OnOrderUpdates(updates);
        }

        private IAnalyticsStorage analyticsStorage;
    }
}
