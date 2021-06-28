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
