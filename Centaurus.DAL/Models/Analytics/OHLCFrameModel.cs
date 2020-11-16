﻿using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models.Analytics
{
    public class OHLCFrameModel
    {
        public int TimeStamp { get; set; }

        public int Market { get; set; }

        public int Period { get; set; }

        public double High { get; set; }

        public double Low { get; set; }

        public double Open { get; set; }

        public double Close { get; set; }

        public double BaseAssetVolume { get; set; }

        public double MarketAssetVolume { get; set; }
    }
}