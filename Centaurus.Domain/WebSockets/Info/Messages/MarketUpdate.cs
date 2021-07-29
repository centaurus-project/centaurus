using Centaurus.Analytics;
using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class MarketUpdate: SuccesResponse
    {
        public int Market { get; set; }

        public OHLCFramePeriod Period { get; set; }

        public List<OHLCFrame> Frames { get; set; }

        public List<Trade> Trades { get; set; }
    }
}