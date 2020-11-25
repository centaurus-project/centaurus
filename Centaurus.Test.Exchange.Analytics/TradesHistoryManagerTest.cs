using Centaurus.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test.Exchange.Analytics
{
    public class TradesHistoryManagerTest : BaseAnalyticsTest
    {
        [Test]
        public void GetTradesTest()
        {
            GenerateTrades(historyLength * 10);

            foreach (var market in markets)
            {
                var trades = analyticsManager.TradesHistoryManager.GetTrades(market);
                Assert.LessOrEqual(historyLength, trades.Count, "Trades history is bigger than max history size.");
                var lastTrade = default(Trade);
                foreach (var trade in trades)
                {
                    if (lastTrade != null)
                        Assert.GreaterOrEqual(lastTrade.TradeDate, trade.TradeDate, "New trades should always be on top of the history.");
                    lastTrade = trade;
                }
            }
        }
    }
}
