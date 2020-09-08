using Centaurus.DAL.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class SettingsModelExtensions
    {
        public static ConstellationSettings ToSettings(this SettingsModel settings, List<AssetModel> assetSettings)
        {
            var assets = new List<AssetSettings>();
            assets.Add(new AssetSettings());//add XLM
            assets.AddRange(assetSettings.Select(a => a.ToAssetSettings()));
            var resultSettings = new ConstellationSettings
            {
                Apex = settings.Apex,
                Auditors = settings.Auditors.Select(a => (RawPubKey)a).ToList(),
                MinAccountBalance = settings.MinAccountBalance,
                MinAllowedLotSize = settings.MinAllowedLotSize,
                Vault = settings.Vault,
                Assets = assets,
                RequestRateLimits = new RequestRateLimits
                {
                    HourLimit = settings.RequestRateLimits.HourLimit,
                    MinuteLimit = settings.RequestRateLimits.MinuteLimit
                }
            };
            return resultSettings;
        }
    }
}
