using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class DepthsSubscription : BaseMarketSubscription
    {
        public override string Name => $"DepthsSubscription@{Market}-{Side}-{Precision}";

        public const double VeryHigh = 0.01;
        public const double High = 0.1;
        public const double Exact = 1;
        public const double Low = 10;
        public const double Lower = 50;
        public const double VeryLow = 100;

        public static double[] Precisions = new double[] { VeryHigh, High, Exact, Low, Lower, VeryLow };

        public DepthsSide Side { get; private set; }

        public double Precision { get; private set; }

        public override bool Equals(object obj)
        {
            return obj is DepthsSubscription subscription &&
                   Market == subscription.Market &&
                   Side == subscription.Side &&
                   Precision == subscription.Precision;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Market, Side, Precision);
        }

        public override void SetValues(string rawValues)
        {
            var values = rawValues.Split('-', StringSplitOptions.RemoveEmptyEntries);


            if (values.Length != 3) //Market, Side and Precision
                throw new ArgumentException("Market, Side or Precision property is not specified.");

            SetMarket(values[0]);

            if (!Enum.TryParse<DepthsSide>(values[1], out var side))
                throw new ArgumentException($"{values[1]} is not valid Side value.");
            Side = side;
            if (!double.TryParse(values[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var precision) || !Precisions.Contains(precision))
                throw new ArgumentException($"{values[2]} is not valid precision value.");
            Precision = precision;

        }
    }
}
