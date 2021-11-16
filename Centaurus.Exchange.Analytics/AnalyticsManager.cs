using Centaurus.Models;
using Centaurus.PersistentStorage;
using Centaurus.PersistentStorage.Abstraction;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;

namespace Centaurus.Exchange.Analytics
{
    public class AnalyticsManager : IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly System.Timers.Timer saveTimer = new System.Timers.Timer();
        private readonly System.Timers.Timer updateTimer = new System.Timers.Timer();

        public AnalyticsManager(IPersistentStorage analyticsStorage, List<double> precisions, List<string> markets, List<OrderInfo> orders, int tradesHistorySize = 100)
        {
            this.analyticsStorage = analyticsStorage ?? throw new ArgumentNullException(nameof(analyticsStorage));
            AnalyticsExchange = AnalyticsExchange.RestoreExchange(markets, orders);
            PriceHistoryManager = new PriceHistoryManager(analyticsStorage, markets ?? throw new ArgumentNullException(nameof(markets)));
            TradesHistoryManager = new TradesHistoryManager(markets, tradesHistorySize);
            MarketDepthsManager = new MarketDepthsManager(markets, precisions, AnalyticsExchange.OrderMap);
            MarketTickersManager = new MarketTickersManager(markets, PriceHistoryManager);
        }

        public void Restore(DateTime dateTime)
        {
            PriceHistoryManager.Restore(dateTime);
            MarketDepthsManager.Restore(dateTime);
        }


        /// <summary>
        /// Initializes and starts save and update timers.
        /// </summary>
        public void StartTimers()
        {
            saveTimer.Interval = 5 * 1000;
            saveTimer.AutoReset = false;
            saveTimer.Elapsed += SaveTimer_Elapsed;
            saveTimer.Start();

            updateTimer.Interval = 1000;
            updateTimer.AutoReset = false;
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
        }

        private readonly object savingSyncRoot = new { };
        public void SaveUpdates(IPersistentStorage analyticsStorage, int maxAttempts = 5)
        {
            lock (savingSyncRoot)
            {
                var frames = PriceHistoryManager.PullUpdates();
                if (frames.Count < 1)
                    return;
                var attempt = 0;
                while (attempt < maxAttempts)
                {
                    try
                    {
                        var frameModels = frames.Select(f => (IPersistentModel)f.ToFramePersistentModel()).ToList();
                        analyticsStorage.SaveBatch(frameModels);
                        break;
                    }
                    catch
                    {
                        attempt++;
                        if (attempt == maxAttempts)
                            throw;
                        Thread.Sleep(attempt * 500);
                    }
                }
            }
        }

        public event Action OnUpdate;

        public event Action<Exception> OnError;

        public PriceHistoryManager PriceHistoryManager { get; }
        public TradesHistoryManager TradesHistoryManager { get; }
        public MarketDepthsManager MarketDepthsManager { get; }
        public MarketTickersManager MarketTickersManager { get; }


        public void Dispose()
        {
            DisposeSaveTimer();

            DisposeUpdateTimer();

            PriceHistoryManager.Dispose();
        }

        private void DisposeSaveTimer()
        {
            lock (savingSyncRoot)
            {
                saveTimer.Stop();
                saveTimer.Dispose();
            }
        }

        private void DisposeUpdateTimer()
        {
            lock (syncRoot)
            {
                updateTimer.Stop();
                updateTimer.Dispose();
            }
        }

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (syncRoot)
            {
                try
                {
                    PriceHistoryManager.Update();
                    MarketTickersManager.Update();
                    updateTimer.Start();
                    OnUpdate?.Invoke();
                }
                catch (Exception exc)
                {
                    OnError?.Invoke(exc);
                }
            }
        }

        private void SaveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                SaveUpdates(analyticsStorage);
                saveTimer.Start();
            }
            catch (Exception exc)
            {
                OnError?.Invoke(exc);
            }
        }


        private readonly object syncRoot = new { };

        public void OnUpdates(ExchangeUpdate updates)
        {
            lock (syncRoot)
            {
                try
                {
                    AnalyticsExchange.OnUpdates(updates);
                    PriceHistoryManager.OnTrade(updates);
                    TradesHistoryManager.OnTrade(updates);
                    MarketDepthsManager.OnOrderUpdates(updates);
                }
                catch (Exception exc)
                {
                    //TODO: add support for delayed trades, now it failes during rising
                    OnError?.Invoke(exc);
                    logger.Error(exc);
                }
            }
        }

        private IPersistentStorage analyticsStorage;

        public AnalyticsExchange AnalyticsExchange { get; }
    }
}
