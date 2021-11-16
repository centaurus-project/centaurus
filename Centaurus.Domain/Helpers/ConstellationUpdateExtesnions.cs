using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class ConstellationUpdateExtesnions
    {
        public static ConstellationSettings ToConstellationSettings(this ConstellationUpdate constellationUpdate, ulong apex)
        {
            return new ConstellationSettings
            {
                Assets = constellationUpdate.Assets,
                Auditors = constellationUpdate.Auditors,
                MinAccountBalance = constellationUpdate.MinAccountBalance,
                MinAllowedLotSize = constellationUpdate.MinAllowedLotSize,
                Providers = constellationUpdate.Providers,
                RequestRateLimits = constellationUpdate.RequestRateLimits,
                Alpha = constellationUpdate.Alpha,
                Apex = apex
            };
        }
    }
}
