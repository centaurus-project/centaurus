using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class AlphaStateHelper
    {
        public static AlphaState GetCurrentState()
        {
            return new AlphaState
            {
                State = Global.AppState.State,
                TxCursor = Global.TxManager.TxCursor
            };
        }
    }
}
