using Centaurus.Analytics;
using Centaurus.DAL.Models.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    public static class TradeExtensions
    {
        public static Trade FromTradeModel(this TradeModel tradeModel)
        {
            return new Trade
            {
                Amount = tradeModel.Amount,
                Asset = tradeModel.Asset,
                BaseAmount = tradeModel.BaseAmount,
                Price = tradeModel.Price,
                Timestamp = tradeModel.Timestamp
            };
        }

        public static TradeModel ToTradeModel(this Trade trade)
        {
            return new TradeModel
            {
                Amount = trade.Amount,
                Asset = trade.Asset,
                BaseAmount = trade.BaseAmount,
                Price = trade.Price,
                Timestamp = trade.Timestamp
            };
        }
    }
}
