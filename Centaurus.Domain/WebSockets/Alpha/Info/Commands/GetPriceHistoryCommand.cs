using Centaurus.Models;
using System;
using System.Text.Json.Serialization;

namespace Centaurus.Domain
{
    [Command("GetPriceHistory")]
    public class GetPriceHistoryCommand: BaseCommand
    {
        public int Market { get; set; }

        public PriceHistoryPeriod Period { get; set; }

        /// <summary>
        /// Unix timestamp
        /// </summary>
        public int Cursor { get; set; }
    }
}