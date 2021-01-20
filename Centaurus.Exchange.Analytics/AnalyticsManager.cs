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

        public AnalyticsManager(IAnalyticsStorage analyticsStorage, List<double> precisions, List<int> markets, List<OrderInfo> orders, int tradesHistorySize = 100)
        {
            this.analyticsStorage = analyticsStorage ?? throw new ArgumentNullException(nameof(analyticsStorage));
            AnalyticsExchange = AnalyticsExchange.RestoreExchange(markets, orders);
            PriceHistoryManager = new PriceHistoryManager(analyticsStorage, markets ?? throw new ArgumentNullException(nameof(markets)));
            TradesHistoryManager = new TradesHistoryManager(markets, tradesHistorySize);
            MarketDepthsManager = new MarketDepthsManager(markets, precisions, AnalyticsExchange.OrderMap);
            MarketTickersManager = new MarketTickersManager(markets, PriceHistoryManager);
        }

        public async Task Restore(DateTime dateTime)
        {
            await PriceHistoryManager.Restore(dateTime);
            MarketDepthsManager.Restore(dateTime);
        }


        /// <summary>
        /// Initializes and starts save and update timers.
        /// </summary>
        public void StartTimers()
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

        private SemaphoreSlim savingSemaphore = new SemaphoreSlim(1);
        public async Task SaveUpdates(IAnalyticsStorage analyticsStorage, int numberOfTries = 5)
        {
            await savingSemaphore.WaitAsync();
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
                savingSemaphore.Release();
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
            syncRoot?.Dispose();
            syncRoot = null;

            savingSemaphore?.Dispose();
            savingSemaphore = null;

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

        private async void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            await syncRoot.WaitAsync();
            try
            {
                PriceHistoryManager.Update();
                await MarketTickersManager.Update();
                updateTimer?.Start();
                OnUpdate?.Invoke();
            }
            catch (Exception exc)
            {
                OnError?.Invoke(exc);
            }
            finally
            {
                syncRoot.Release();
            }
        }

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


        private SemaphoreSlim syncRoot = new SemaphoreSlim(1);

        public async Task OnUpdates(ExchangeUpdate updates)
        {
            await syncRoot.WaitAsync();
            try
            {
                AnalyticsExchange.OnUpdates(updates);
                await PriceHistoryManager.OnTrade(updates);
                TradesHistoryManager.OnTrade(updates);
                MarketDepthsManager.OnOrderUpdates(updates);
            }
            catch (Exception exc)
            {
                //TODO: add support for delayed trades, now it failes during rising
                OnError?.Invoke(exc);
                logger.Error(exc);
            }
            finally
            {
                syncRoot.Release();
            }
        }

        private IAnalyticsStorage analyticsStorage;

        public AnalyticsExchange AnalyticsExchange { get; }
    }
}
