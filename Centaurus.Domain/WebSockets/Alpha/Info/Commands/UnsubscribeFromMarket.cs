using Centaurus.Analytics;

namespace Centaurus.Domain
{
    [Command("UnsubscribeFromMarket")]
    public class UnsubscribeFromMarket: BaseCommand
    {
        public int Market { get; set; }

        public OHLCFramePeriod Period { get; set; }
    }
}