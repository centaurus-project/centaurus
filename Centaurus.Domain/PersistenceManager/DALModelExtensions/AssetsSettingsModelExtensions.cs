using Centaurus.DAL.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class AssetsSettingsModelExtensions
    {
        public static AssetSettings ToAssetSettings(this AssetModel asset)
        {
            return new AssetSettings { Code = asset.Code, Id = asset.Id, IsSuspended = asset.IsSuspended };
        }

        public static AssetModel ToAssetModel(this AssetSettings asset)
        {
            return new AssetModel { Code = asset.Code, Id = asset.Id, IsSuspended = asset.IsSuspended };
        }
    }
}
