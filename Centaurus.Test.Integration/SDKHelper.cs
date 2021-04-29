using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SdkConstellationInfo = Centaurus.SDK.Models.ConstellationInfo;
using SdkAsset = Centaurus.SDK.Models.ConstellationInfo.Asset;
using SdkState = Centaurus.SDK.Models.ConstellationInfo.ApplicationStateModel;
using Network = Centaurus.SDK.Models.ConstellationInfo.Network;

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
                    .Select(a => new SdkAsset { Code = a.Code, Id = a.Id, Issuer = a.Issuer })
                    .ToArray(),
                Auditors = info.Auditors,
                MinAccountBalance = info.MinAccountBalance,
                MinAllowedLotSize = info.MinAllowedLotSize,
                RequestRateLimits = info.RequestRateLimits,
                State = (SdkState)(int)info.State,
                Vault = info.Vault,
                StellarNetwork = new Network(info.StellarNetwork.Passphrase, info.StellarNetwork.Horizon)
            };
        }
    }
}
