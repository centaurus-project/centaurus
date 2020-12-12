using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Centaurus.SDK.Models
{
    public class BalanceModel
    {
        public int AssetId { get; set; }
        public string Asset { get; set; }
        public long Amount { get; set; }
        public long Liabilities { get; set; }

        public decimal AmountInXlm
        {
            get => Amount > 0 ? decimal.Divide(Amount, StroopsPerXlm) : 0;
        }

        public string AmountStr => AmountInXlm.ToString("0.#########", CultureInfo.InvariantCulture);

        public decimal LiabilitiesInXlm
        {
            get => Liabilities > 0 ? decimal.Divide(Liabilities, StroopsPerXlm) : 0;
        }

        public string LiabilitiesStr => LiabilitiesInXlm.ToString("0.#########", CultureInfo.InvariantCulture);

        public const int StroopsPerXlm = 10000000;

        public static BalanceModel FromBalance(Balance balance, ConstellationInfo constellationInfo)
        {
            return new BalanceModel
            {
                AssetId = balance.Asset,
                Asset = (constellationInfo.Assets.FirstOrDefault(a => a.Id == balance.Asset)?.DisplayName ?? balance.Asset.ToString()),
                Amount = balance.Amount,
                Liabilities = balance.Liabilities
            };
        }
    }
}
