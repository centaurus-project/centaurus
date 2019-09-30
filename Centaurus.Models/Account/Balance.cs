using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class Balance: IXdrSerializableModel
    {
        public int Asset { get; set; }

        public long Amount { get; set; }

        public long Liabilities { get; set; }

        public override string ToString()
        {
            return $"Asset: {Asset}, amount: {Amount}, liabilities: {Liabilities}.";
        }
    }
}
