using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;

namespace Centaurus.Domain
{
    public static class AssetsSettingsModelExtensions
    {
        public static AssetSettings ToDomainModel(this AssetPersistentModel asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));
            return new AssetSettings
            {
                Code = asset.Code,
                IsSuspended = asset.IsSuspended,
                IsQuoteAsset = asset.IsQuoteAsset
            };
        }

        public static AssetPersistentModel ToPersistentModel(this AssetSettings asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));
            return new AssetPersistentModel
            {
                Code = asset.Code,
                IsSuspended = asset.IsSuspended,
                IsQuoteAsset = asset.IsQuoteAsset
            };
        }
    }
}
