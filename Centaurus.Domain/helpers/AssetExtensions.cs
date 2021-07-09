using Centaurus.Models;

namespace Centaurus.Domain
{
    public static class AssetExtensions
    {
        public static ExchangeMarket CreateMarket(this AssetSettings asset, OrderMap orderMap, bool useLegacyOrderbook = false)
        {
            return new ExchangeMarket(asset.Code, orderMap, useLegacyOrderbook);
        }
    }
}
