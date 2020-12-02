using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class PriceHistorySubscription : BaseMarketSubscription
    {
        public override string Name => $"PriceHistorySubscription@{Market}-{FramePeriod}";

        public PriceHistoryPeriod FramePeriod { get; set; }

        public override bool Equals(object obj)
        {
            return obj is PriceHistorySubscription subscription &&
                   Market == subscription.Market &&
                   FramePeriod == subscription.FramePeriod;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Market, FramePeriod);
        }

        public override void SetValues(string rawValues)
        {
            var values = rawValues.Split('-', StringSplitOptions.RemoveEmptyEntries);

            if (values.Length != 2) //Market, FramePeriod
                throw new ArgumentException("Market or FramePeriod property is not specified.");

            SetMarket(values[0]);
            if (!Enum.TryParse<PriceHistoryPeriod>(values[1], out var framePeriod))
                throw new ArgumentException($"{framePeriod} is not valid FramePeriod value.");
            FramePeriod = framePeriod;
        }
    }
}