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
            return new EffectsResponse
            {
                Items = effects.Items.Select(e => XdrConverter.Deserialize<Effect>(e.RawEffect)).ToList(),
                CurrentToken = effects.CurrentToken,
                NextToken = effects.NextToken,
                PrevToken = effects.PrevToken
            };
        }
    }
}
