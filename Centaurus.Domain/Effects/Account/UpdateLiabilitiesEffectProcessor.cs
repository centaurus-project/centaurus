using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class UpdateLiabilitiesEffectProcessor : BaseAccountEffectProcessor<UpdateLiabilitiesEffect>
    {
        public UpdateLiabilitiesEffectProcessor(UpdateLiabilitiesEffect effect, Account account)
            : base(effect, account)
        {

        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            var balance = Account.GetBalance(Effect.Asset);
            balance.UpdateLiabilities(Effect.Amount);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            Account.GetBalance(Effect.Asset).UpdateLiabilities(Effect.Amount);
        }
    }
}
