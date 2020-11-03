using Centaurus.Analytics;
using System;

namespace Centaurus.Domain
{
    [Command("GetMarket")]
    public class GetMarket: BaseCommand
    {
        public int Market { get; set; }

        public OHLCFramePeriod Period { get; set; }

        public bool SubscribeToUpdates { get; set; }

        public DateTime DateFrom { get; set; }

        public DateTime DateTo { get; set; }

        public int Limit { get; set; } = 100;
    }
}