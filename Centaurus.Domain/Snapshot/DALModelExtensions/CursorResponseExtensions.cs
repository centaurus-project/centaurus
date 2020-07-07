using Centaurus.DAL.Models;
using Centaurus.Models;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class CursorResponseExtensions
    {
        public static EffectsResponse ToEffectResponse(this CursorResult<EffectModel> effects)
        {
            var parsedEffects = new List<Effect>();
            foreach (var e in effects.Items)
            {
                var effect = XdrConverter.Deserialize<Effect>(e.RawEffect);
                if (e.Account != null)
                    effect.Pubkey = e.Account;
                parsedEffects.Add(effect);
            }

            return new EffectsResponse
            {
                Items = parsedEffects,
                CurrentToken = effects.CurrentToken,
                NextToken = effects.NextToken,
                PrevToken = effects.PrevToken
            };
        }
    }
}
