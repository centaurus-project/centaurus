using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    [XdrContract]
    public class Balance
    {
        [XdrField(0)]
        public string Asset { get; set; }

        [XdrField(1)]
        public ulong Amount { get; set; }

        [XdrField(2)]
        public ulong Liabilities { get; set; }

        public override string ToString()
        {
            return $"Asset: {Asset}, amount: {Amount}, liabilities: {Liabilities}.";
        }
    }
}
