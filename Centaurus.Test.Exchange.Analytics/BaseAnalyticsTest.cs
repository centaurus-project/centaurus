using Centaurus.Analytics;
using Centaurus.Exchange.Analytics;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Test.Exchange.Analytics
{
    public class BaseAnalyticsTest
    {
        protected AnalyticsManager analyticsManager;
        protected MockStorage storage;
        protected List<int> markets;
        protected int historyLength;
        protected long analyticsStartDate;
        protected long now;

        public BaseAnalyticsTest()
        {
            analyticsStartDate = DateTime.UtcNow.Ticks;
            now = analyticsStartDate;
        }

        [SetUp]
        public void Setup()
        {
            storage = new MockStorage();
            markets = Enumerable.Range(1, 2).ToList();
            historyLength = 100;
            analyticsManager = new AnalyticsManager(storage, new List<double> { 1 }, new MockOrderMap(), markets, historyLength);
        }

        protected void GenerateTrades(int totalTradesCount)
        {
            var r = new Random();
            for (int i = 0; i < totalTradesCount; i++)
            {
                var minPrice = 1;
                var trades = new List<Trade>();
                var tradesCount = r.Next(1, 20);
                for (var c = 0; c < tradesCount; c++)
                {
                    var amount = r.Next(1, 1000);
                    var price = r.Next(minPrice, 1000);
                    trades.Add(new Trade
                    {
                        Amount = amount,
                        Asset = markets[r.Next(0, markets.Count)],
                        Price = price,
                        BaseAmount = amount * price,
                        Timestamp = now
                    });
                    if (minPrice == 0)
                        minPrice = price;
                    else
                        minPrice = Math.Min(minPrice, price);
                    now += 1;
                }
                var groupedTrades = trades.GroupBy(t => t.Asset);
                foreach (var g in groupedTrades)
                {
                    var updates = new ExchangeUpdate(g.Key);
                    updates.Trades.AddRange(g);
                    analyticsManager.OnUpdates(updates);
                }    
                now += TimeSpan.TicksPerSecond * 20;
            }
        }
    }
}
