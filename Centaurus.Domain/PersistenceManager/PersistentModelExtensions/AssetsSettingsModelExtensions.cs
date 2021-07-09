using Centaurus.Models;
using Centaurus.PersistentStorage;

namespace Centaurus.Domain
{
    public static class AssetsSettingsModelExtensions
    {
        public static AssetSettings ToDomainModel(this AssetPersistentModel asset)
        {
            return new AssetSettings { Code = asset.Code, IsSuspended = asset.IsSuspended };
        }

        public static AssetPersistentModel ToPersistentModel(this AssetSettings asset)
        {
            return new AssetPersistentModel { Code = asset.Code, IsSuspended = asset.IsSuspended };
        }
    }
}
