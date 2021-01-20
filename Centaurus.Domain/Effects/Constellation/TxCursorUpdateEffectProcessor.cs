using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class TxCursorUpdateEffectProcessor : EffectProcessor<TxCursorUpdateEffect>
    {
        private TxCursorManager txManager;

        public TxCursorUpdateEffectProcessor(TxCursorUpdateEffect effect, TxCursorManager txManager)
            : base(effect)
        {
            this.txManager = txManager;
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            txManager.SetCursor(Effect.Cursor);
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            txManager.SetCursor(Effect.PrevCursor);
        }
    }
}
