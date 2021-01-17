using Centaurus.DAL;
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
        public static EffectModel FromEffect(this Effect effect, int index, long timestamp)
        {
            return new EffectModel
            {
                Id = EffectModelIdConverter.EncodeId(effect.Apex, index),
                Account = effect.Account,
                EffectType = (int)effect.EffectType,
                RawEffect = XdrConverter.Serialize(effect),
                Timestamp = timestamp
            };
        }

        public static Effect ToEffect(this EffectModel effectModel)
        {
            var effect = XdrConverter.Deserialize<Effect>(effectModel.RawEffect);
            effect.Account = effectModel.Account;
            return effect;
        }
    }
}
