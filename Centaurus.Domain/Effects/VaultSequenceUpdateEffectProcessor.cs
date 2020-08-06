using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class VaultSequenceUpdateEffectProcessor : EffectProcessor<VaultSequenceUpdateEffect>
    {
        public VaultSequenceUpdateEffectProcessor(VaultSequenceUpdateEffect effect, AccountData vaultAccountData)
            :base(effect)
        {
            this.vaultAccountData = vaultAccountData ?? throw new ArgumentNullException(nameof(vaultAccountData));
        }

        public AccountData vaultAccountData;

        public override void CommitEffect()
        {
            MarkAsProcessed();
            vaultAccountData.SetSequence(Effect.Sequence);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            vaultAccountData.SetSequence(Effect.PrevSequence);
        }
    }
}
