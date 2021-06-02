using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class TxCursorUpdateEffectProcessor : EffectProcessor<CursorUpdateEffect>
    {
        private PaymentNotificationManager notificationManager;

        public TxCursorUpdateEffectProcessor(CursorUpdateEffect effect, PaymentNotificationManager notificationManager)
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
