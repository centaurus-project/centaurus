using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class ExchangeUpdate
    {
        public ExchangeUpdate(int market)
        {
            Market = market;
        }

        public List<Trade> Trades { get; } = new List<Trade>();

        public List<OrderInfo> OrderUpdates { get; } = new List<OrderInfo>();

        public int Market { get; }
    }
}