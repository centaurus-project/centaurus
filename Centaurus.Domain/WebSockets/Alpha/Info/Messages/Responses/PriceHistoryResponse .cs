using Centaurus.Models;
using Centaurus.Exchange.Analytics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class PriceHistoryResponse : SuccesResponse
    {
        public List<PriceHistoryFrame> PriceHistory  { get; set; }

        public int NextCursor { get; set; }
    }
}