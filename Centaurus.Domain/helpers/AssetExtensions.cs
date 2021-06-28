using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class AssetExtensions
    {
        public static ExchangeMarket CreateMarket(this AssetSettings asset, OrderMap orderMap, bool useLegacyOrderbook = false)
        {
            return new ExchangeMarket(asset.Id, orderMap, useLegacyOrderbook);
        }
    }
}
