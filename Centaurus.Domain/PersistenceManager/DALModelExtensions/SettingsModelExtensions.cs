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
        public static ConstellationSettings ToSettings(this SettingsModel settings)
        {
            var assets = new List<AssetSettings>();
            assets.Add(new AssetSettings());//add XLM
            assets.AddRange(settings.Assets.Select(a => a.ToAssetSettings()).OrderBy(a => a.Id));
            var resultSettings = new ConstellationSettings
            {
                Apex = settings.Apex,
                Auditors = settings.Auditors.Select(a => (RawPubKey)a).OrderBy(a => a.ToString()).ToList(),
                MinAccountBalance = settings.MinAccountBalance,
                MinAllowedLotSize = settings.MinAllowedLotSize,
                Providers = settings.Providers.Select(pm => new ProviderSettings
                {
                    Provider = pm.Provider,
                    Name = pm.Name,
                    Cursor = pm.Cursor,
                    PaymentSubmitDelay = pm.PaymentSubmitDelay,
                    Vault = pm.Vault,
                    Assets = pm.Assets
                        .Select(am => new ProviderAsset
                        {
                            CentaurusAsset = am.CentaurusAsset,
                            Token = am.Token,
                            IsVirtual = am.IsVirtual
                        })
                        .ToList()
                }).ToList(),
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
