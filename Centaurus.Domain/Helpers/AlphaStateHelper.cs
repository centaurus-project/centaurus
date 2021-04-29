using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class AlphaStateHelper
    {
        public static AlphaState GetCurrentState(this ExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            return new AlphaState
            {
                State = context.AppState.State,
                TxCursor = context.TxCursorManager.TxCursor
            };
        }
    }
}
