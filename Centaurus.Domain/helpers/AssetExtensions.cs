using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class AssetExtensions
    {
        public static Market CreateMarket(this AssetSettings asset, OrderMap orderMap)
        {
            return new Market(asset.Id, orderMap);
        }

        public static Asset ToAsset(this AssetSettings assetSettings)
        {
            if (assetSettings.IsXlm) return new AssetTypeNative();
            return Asset.CreateNonNativeAsset(assetSettings.Code, assetSettings.Issuer.ToString());
        }
    }
}
