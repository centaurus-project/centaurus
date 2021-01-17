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
            return new AssetSettings { Code = asset.Code, Id = asset.Id, Issuer = asset.Issuer };
        }
    }
}
