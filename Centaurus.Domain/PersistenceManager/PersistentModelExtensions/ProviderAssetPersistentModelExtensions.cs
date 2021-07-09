using Centaurus.Models;
using Centaurus.PersistentStorage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class ProviderAssetPersistentModelExtensions
    {
        public static ProviderAssetPersistentModel ToPersistentModel(this ProviderAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            return new ProviderAssetPersistentModel { CentaurusAsset = asset.CentaurusAsset, Token = asset.Token, IsVirtual = asset.IsVirtual };
        }

        public static ProviderAsset ToDomainModel(this ProviderAssetPersistentModel asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            return new ProviderAsset { CentaurusAsset = asset.CentaurusAsset, Token = asset.Token, IsVirtual = asset.IsVirtual };
        }
    }
}
