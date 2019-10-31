using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class Balance
    {
        [XdrField(0)]
        public int Asset { get; set; }

        [XdrField(1)]
        public long Amount { get; set; }

        [XdrField(2)]
        public long Liabilities { get; set; }

        public override string ToString()
        {
            return $"Asset: {Asset}, amount: {Amount}, liabilities: {Liabilities}.";
        }
    }
}
