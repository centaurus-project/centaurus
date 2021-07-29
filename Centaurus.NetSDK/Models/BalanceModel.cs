using System.Globalization;

namespace Centaurus.NetSDK
{
    public class BalanceModel
    {
        public int AssetId { get; internal set; }
        public long Amount { get; internal set; }
        public long Liabilities { get; internal set; }

        public decimal AdjustedAmount
        {
            get => Amount > 0 ? decimal.Divide(Amount, AdjustedPrecision) : 0;
        }

        public string AmountAsString => AdjustedAmount.ToString("0.#########", CultureInfo.InvariantCulture);

        public decimal AdjustedLiabilities
        {
            get => Liabilities > 0 ? decimal.Divide(Liabilities, AdjustedPrecision) : 0;
        }

        public string LiabilitiesAsString => AdjustedLiabilities.ToString("0.#########", CultureInfo.InvariantCulture);

        private const int AdjustedPrecision = 10_000_000;
    }
}
