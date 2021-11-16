using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class BalanceUpdateEffect: BaseBalanceEffect
    {
        [XdrField(0)]
        public UpdateSign Sign { get; set; }

        [XdrField(1)]
        public ulong Amount { get; set; }
    }

    public enum UpdateSign
    {
        Plus,
        Minus
    }

    public static class UpdateSignExtensions
    {
        public static UpdateSign Opposite(this UpdateSign sign)
        {
            return sign == UpdateSign.Plus ? UpdateSign.Minus : UpdateSign.Plus;
        }
    }
}
