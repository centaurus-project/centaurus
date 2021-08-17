using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class RawEffectsDataContainerExtensions
    {
        public static List<EffectsInfoBase> GetAccountEffects(this List<RawEffectsDataContainer> rawEffects, ulong accountId)
        {
            if (rawEffects == null)
                throw new ArgumentNullException(nameof(rawEffects));

            //get account effects or all effects for auditors
            return rawEffects.Select(re =>
            {
                if (accountId == 0 || re.Account == accountId)
                    return (EffectsInfoBase)new EffectsInfo { EffectsGroupData = re.RawEffects };
                else
                    return new EffectsHashInfo { EffectsGroupData = re.Hash };
            }).ToList();
        }
    }
}
