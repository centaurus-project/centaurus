using Centaurus.DAL.Models;
using Centaurus.Models;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class EffectModelExtensions
    {
        public static EffectModel FromEffect(this Effect effect, int index)
        {
            return new EffectModel
            {
                Id = BitConverter.GetBytes(effect.Apex).Concat(BitConverter.GetBytes(index)).ToArray(),
                Apex = effect.Apex,
                Account = effect.Pubkey?.Data,
                EffectType = (int)effect.EffectType,
                RawEffect = XdrConverter.Serialize(effect),
                Timestamp = DateTime.UtcNow
            };
        }

        public static Effect ToEffect(this EffectModel effectModel)
        {
            var effect = XdrConverter.Deserialize<Effect>(effectModel.RawEffect);
            if (effectModel.Account != null)
                effect.Pubkey = effectModel.Account;
            return effect;
        }
    }
}
