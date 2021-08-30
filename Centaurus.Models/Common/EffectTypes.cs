using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Models
{
    public enum EffectTypes
    {
        Undefined = 0,

        AccountCreate = 1,
        NonceUpdate = 2,
        BalanceCreate = 3,
        BalanceUpdate = 4,
        RequestRateLimitUpdate = 6,
        AccountSequenceIncEffect = 7,

        OrderPlaced = 10,
        OrderRemoved = 11,
        Trade = 12,

        ConstellationUpdate = 31,

        TxCursorUpdate = 51
    }
}
