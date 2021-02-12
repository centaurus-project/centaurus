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
        public static EffectsModel FromEffect(long apex, int account)
        {
            return new EffectsModel
            {
                Id = EffectModelIdConverter.EncodeId(apex, account),
                Account = account,
                Apex = apex,
                Effects = new List<SingleEffectModel>()
            };
        }

        public static List<Effect> ToEffects(this EffectsModel effectsModel)
        {
            var account = effectsModel.Account;
            return effectsModel.Effects.Select(e => e.ToEffect(account)).ToList();
        }

        public static SingleEffectModel FromEffect(this Effect effect, int index)
        {
            return new SingleEffectModel
            {
                ApexIndex = index,
                RawEffect = XdrConverter.Serialize(effect)
            };
        }

        public static Effect ToEffect(this SingleEffectModel effectModel, int account)
        {
            var effect = XdrConverter.Deserialize<Effect>(effectModel.RawEffect);
            effect.Account = account;
            return effect;
        }
    }
}
