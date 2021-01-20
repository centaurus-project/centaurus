using Centaurus.Exchange.Analytics;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Centaurus.Models;
using System.Text;

namespace Centaurus.Test.Exchange.Analytics
{
    public class BaseAnalyticsTest
    {
        protected AnalyticsManager analyticsManager;
        protected MockStorage storage;
        protected List<int> markets;
        protected int historyLength;
        protected DateTime now;

        public BaseAnalyticsTest()
        {
            now = DateTime.UtcNow;
        }

        [SetUp]
        public void Setup()
        {
            storage = new MockStorage();
            markets = Enumerable.Range(1, 2).ToList();
            historyLength = 100;
            analyticsManager = new AnalyticsManager(storage, new List<double> { 1 }, markets, new List<OrderInfo>(), historyLength);
        }

        protected void GenerateTrades(int totalTradesCount)
        {
            var r = new Random();
            for (int i = 0; i < totalTradesCount; i++)
            {
                var minPrice = 1;
                var trades = new List<Trade>();
                var tradesCount = r.Next(1, 20);
                var market = markets[r.Next(0, markets.Count)];
                for (var c = 0; c < tradesCount; c++)
                {
                    var amount = r.Next(1, 1000);
                    var price = r.Next(minPrice, 1000);
                    trades.Add(new Trade
                    {
                        Amount = amount,
                        Asset = market,
                        Price = price,
                        BaseAmount = amount * price,
                        TradeDate = now
                    });
                    if (minPrice == 0)
                        minPrice = price;
                    else
                        minPrice = Math.Min(minPrice, price);
                }
                var updates = new ExchangeUpdate(market, now);
                updates.Trades.AddRange(trades);
                analyticsManager.OnUpdates(updates).Wait();
                now = now.AddSeconds(20);
            }
        }
    }
}
