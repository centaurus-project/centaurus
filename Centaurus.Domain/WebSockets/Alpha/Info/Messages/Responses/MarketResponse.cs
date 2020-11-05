using Centaurus.Analytics;
using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class MarketResponse: SuccesResponse
    {
        public List<OHLCFrame> Frames { get; set; }

        public List<Trade> Trades { get; set; }

        public int NextCursor { get; set; }
    }
}
