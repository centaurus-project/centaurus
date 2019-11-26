using Centaurus.DAL.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class AssetsSettingsModelExtensions
    {
        public static AssetSettingsModel FromAssetSettings(AssetSettings asset)
        {
            return new AssetSettingsModel { Code = asset.Code, AssetId = asset.Id, Issuer = asset.Issuer.Data };
        }

        public static AssetSettings ToAssetSettings(this AssetSettingsModel asset)
        {
            return new AssetSettings { Code = asset.Code, Id = asset.AssetId, Issuer = asset.Issuer };
        }
    }
}
