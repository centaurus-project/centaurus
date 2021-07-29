using Centaurus.Models;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class AssetModelExtensions
    {
        public static AssetModel ToProviderModel(this ProviderAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));
            return new AssetModel { CentaurusAsset = asset.CentaurusAsset, IsVirtual = asset.IsVirtual, Token = asset.Token };
        }

        public static ProviderAsset ToDomainModel(this AssetModel asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));
            return new ProviderAsset { CentaurusAsset = asset.CentaurusAsset, IsVirtual = asset.IsVirtual, Token = asset.Token };
        }
    }
}
