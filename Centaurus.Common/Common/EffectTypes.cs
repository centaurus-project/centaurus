using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus
{
    public enum EffectTypes
    {
        Undefined = 0,

        AccountCreate = 1,
        NonceUpdate = 2,
        BalanceCreate = 3,
        BalanceUpdate = 4,
        LockLiabilities = 5,
        UnlockLiabilities = 6,
        RequestRateLimitUpdate = 7,

        OrderPlaced = 10,
        OrderRemoved = 11,
        Trade = 12,

        AssetSent = 21,
        AssetReceived = 22,
        WithdrawalCreate = 23,
        WithdrawalRemove = 24,

        ConstellationInit = 31,
        ConstellationUpdate = 32,

        LedgerUpdate = 51
    }
}
