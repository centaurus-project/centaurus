using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class NonceUpdateEffect: AccountEffect
    {
        [XdrField(0)]
        public ulong Nonce { get; set; }

        [XdrField(1)]
        public ulong PrevNonce { get; set; }
    }
}
