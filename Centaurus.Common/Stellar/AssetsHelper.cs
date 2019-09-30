using stellar_dotnet_sdk;
using System;

namespace Centaurus
{
    public static class AssetsHelper
    {
        public static Asset BuildAssetFromSymbol(string assetSymbol)
        {
            if (assetSymbol == "XLM") return new AssetTypeNative();
            string[] parts = assetSymbol.Split('-');
            return Asset.CreateNonNativeAsset(parts[0], parts[1]);
        }
    }
}
