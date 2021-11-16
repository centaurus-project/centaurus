using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SdkConstellationInfo = Centaurus.NetSDK.ConstellationInfo;
using SdkAsset = Centaurus.NetSDK.ConstellationInfo.Asset;
using SdkAuditor = Centaurus.NetSDK.ConstellationInfo.Auditor;
using SdkState = Centaurus.NetSDK.ConstellationInfo.StateModel;
using SdkProviderSettings = Centaurus.NetSDK.ConstellationInfo.ProviderSettings;
using SdkProviderAsset = Centaurus.NetSDK.ConstellationInfo.ProviderSettings.ProviderAsset;

namespace Centaurus
{
    public static class SDKHelper
    {
        public static SdkConstellationInfo ToSdkModel(this ConstellationInfo info)
        {
            if (info == null)
                return null;

            return new SdkConstellationInfo
            {
                Assets = info.Assets
                    .Select(a => new SdkAsset { Code = a.Code, IsSuspended = a.IsSuspended })
                    .ToList(),
                Auditors = info.Auditors.Select(a =>
                    new SdkAuditor { Address = a.Address, PubKey = a.PubKey }).ToList(),
                MinAccountBalance = info.MinAccountBalance,
                MinAllowedLotSize = info.MinAllowedLotSize,
                RequestRateLimits = info.RequestRateLimits,
                State = (SdkState)(int)info.State,
                Providers = info.Providers.Select(p => new SdkProviderSettings
                {
                    Provider = p.Provider,
                    Name = p.Name,
                    Vault = p.Vault,
                    Assets = p.Assets.Select(a =>
                        new SdkProviderAsset { CentaurusAsset = a.CentaurusAsset, Token = a.Token }).ToList()
                }).ToList()
            };
        }
    }
}
