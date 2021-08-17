using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class AccountCreateEffect: AccountEffect
    {
        [XdrField(0)]
        public RawPubKey Pubkey { get; set; }
    }
}