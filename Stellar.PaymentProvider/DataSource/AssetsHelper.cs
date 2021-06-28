using stellar_dotnet_sdk;
using System;

namespace Centaurus
{
    public static class AssetsHelper
    {
        public static Asset BuildAssetFromSymbol(string assetSymbol)
        {
            if (assetSymbol == XLMCode) return new AssetTypeNative();
            string[] parts = assetSymbol.Split('-');
            return Asset.CreateNonNativeAsset(parts[0], parts[1]);
        }

        public const int StroopsPerAsset = 10_000_000;

        public const string XLMCode = "XLM";

        public static string GetCode(string code, string issuer)
        {
            if (issuer == null)
                return XLMCode;
            return $"{code}-{issuer}";
        }
    }
}
