using Centaurus.Analytics;
using System;
using System.Text.Json.Serialization;

namespace Centaurus.Domain
{
    [Command("GetMarket")]
    public class GetMarket: BaseCommand
    {
        public int Market { get; set; }

        public OHLCFramePeriod Period { get; set; }

        public bool SubscribeToUpdates { get; set; }

        /// <summary>
        /// Unix timestamp
        /// </summary>
        public int Cursor { get; set; }
    }
}