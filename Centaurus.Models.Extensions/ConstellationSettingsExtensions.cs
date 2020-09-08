using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus
{
    public static class ConstellationSettingsExtensions
    {
        public static bool TryFindAssetSettings(this ConstellationSettings constellationSettings, Asset asset, out AssetSettings assetSettings)
        {
            assetSettings = null;
            if (constellationSettings == null || asset == null)
                return false;
            if (asset is AssetTypeNative)
                assetSettings = constellationSettings.Assets.First(a => a.IsXlm);
            else
            {
                var nonNativeAsset = (AssetTypeCreditAlphaNum)asset;
                if (!(string.IsNullOrWhiteSpace(nonNativeAsset.Code) || string.IsNullOrWhiteSpace(nonNativeAsset.Issuer)))
                    assetSettings = constellationSettings.Assets.FirstOrDefault(a => a.Code == nonNativeAsset.Code && a.Issuer?.ToString() == nonNativeAsset.Issuer);
            }
            return assetSettings != null;
        }

        public static bool TryFindAssetSettings(this ConstellationSettings constellationSettings, int assetId, out AssetSettings assetSettings)
        {
            assetSettings = null;
            if (constellationSettings == null)
                return false;
            assetSettings = constellationSettings.Assets.FirstOrDefault(a => a.Id == assetId);
            return assetSettings != null;
        }
    }
}
