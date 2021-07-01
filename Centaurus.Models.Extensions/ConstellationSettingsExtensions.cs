using Centaurus.Models;
using System.Linq;

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
