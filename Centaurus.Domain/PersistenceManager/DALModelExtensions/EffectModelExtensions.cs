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
                Effects = new List<AtomicEffectModel>()
            };
        }

        public static AtomicEffectModel FromEffect(this Effect effect, int index)
        {
            return new AtomicEffectModel
            {
                ApexIndex = index,
                RawEffect = XdrConverter.Serialize(effect)
            };
        }

        public static Effect ToEffect(this AtomicEffectModel effectModel, AccountWrapper accountWrapper)
        {
            var effect = XdrConverter.Deserialize<Effect>(effectModel.RawEffect);
            effect.AccountWrapper = accountWrapper;
            return effect;
        }
    }
}
