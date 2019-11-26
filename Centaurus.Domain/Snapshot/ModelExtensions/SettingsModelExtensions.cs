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
        public static SettingsModel FromSettings(ConstellationSettings settings)
        {
            return new SettingsModel
            {
                Apex = settings.Apex,
                Auditors = settings.Auditors.Select(a => a.Data).ToArray(),
                MinAccountBalance = settings.MinAccountBalance,
                MinAllowedLotSize = settings.MinAllowedLotSize,
                Vault = settings.Vault.Data
            };
        }

        public static ConstellationSettings ToSettings(this SettingsModel settings, List<AssetSettingsModel> assetSettings)
        { 
            return new ConstellationSettings
            {
                Apex = settings.Apex,
                Auditors = settings.Auditors.Select(a => (RawPubKey)a).ToList(),
                MinAccountBalance = settings.MinAccountBalance,
                MinAllowedLotSize = settings.MinAllowedLotSize,
                Vault = settings.Vault,
                Assets = assetSettings.Select(a => a.ToAssetSettings()).ToList()
            };
        }
    }
}
