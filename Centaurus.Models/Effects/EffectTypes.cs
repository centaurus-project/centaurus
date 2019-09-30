using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Models
{

    public enum EffectTypes
    {
        Undefined = 0,

        AssetSent = 1,
        AssetReceived = 2,

        OrderPlaced = 10,
        OrderRemoved = 11,
        Trade = 12,

        TransactionSigned = 100
    }
}
