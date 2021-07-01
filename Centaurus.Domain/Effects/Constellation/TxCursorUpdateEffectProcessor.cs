using Centaurus.Models;
using Centaurus.PaymentProvider;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class TxCursorUpdateEffectProcessor : EffectProcessor<CursorUpdateEffect>
    {
        private DepositNotificationManager notificationManager;

        public TxCursorUpdateEffectProcessor(CursorUpdateEffect effect, DepositNotificationManager notificationManager)
            : base(effect)
        {
            this.notificationManager = notificationManager;
        }

        public override void CommitEffect()
        {
            MarkAsProcessed();
            notificationManager.Cursor = Effect.Cursor;
        }

        public override void RevertEffect()
        {
            MarkAsProcessed();
            notificationManager.Cursor = Effect.PrevCursor;
        }
    }
}
