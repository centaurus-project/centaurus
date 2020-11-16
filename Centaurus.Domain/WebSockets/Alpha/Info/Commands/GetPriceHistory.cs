﻿using Centaurus.Analytics;
using System;
using System.Text.Json.Serialization;

namespace Centaurus.Domain
{
    [Command("GetPriceHistory")]
    public class GetPriceHistory: BaseCommand
    {
        public int Market { get; set; }

        public OHLCFramePeriod Period { get; set; }

        /// <summary>
        /// Unix timestamp
        /// </summary>
        public int Cursor { get; set; }
    }
}