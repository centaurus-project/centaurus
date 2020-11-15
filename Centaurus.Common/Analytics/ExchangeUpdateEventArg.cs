using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Analytics
{
    public class ExchangeUpdate
    {
        public ExchangeUpdate(int market)
        {
            Market = market;
        }

        public List<Trade> Trades { get; } = new List<Trade>();

        public List<OrderUpdate> OrderUpdates { get; } = new List<OrderUpdate>();

        public int Market { get; }
    }
}