using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class WithdrawalBaseEffect: Effect
    {
        [XdrField(0)]
        public string Provider { get; set; }

        [XdrField(1)]
        public List<WithdrawalEffectItem> Items { get; set; }
    }

    [XdrContract]
    public class WithdrawalEffectItem
    {
        [XdrField(0)]
        public int Asset { get; set; }

        [XdrField(1)]
        public long Amount { get; set; }
    }
}