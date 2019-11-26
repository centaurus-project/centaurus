using Centaurus.DAL.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class EffectModelExtensions
    {
        public static EffectModel FromEffect(Effect effect, int index)
        {
            return new EffectModel
            {
                Index = index,
                Apex = effect.Apex,
                Account = effect.Pubkey.Data,
                EffectType = (int)effect.EffectType,
                RawEffect = XdrConverter.Serialize(effect),
                Timestamp = DateTime.UtcNow
            };
        }

        public static Effect ToAccount(this EffectModel effectModel)
        {
            return XdrConverter.Deserialize<Effect>(effectModel.RawEffect);
        }
    }
}
