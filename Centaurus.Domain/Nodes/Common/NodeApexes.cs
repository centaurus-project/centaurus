namespace Centaurus.Domain.StateManagers
{
    internal class NodeApexes : ValueAggregation<ulong>
    {
        public NodeApexes()
            :base(20)
        {

        }

        /// <summary>
        /// Calculates avg quanta per second
        /// </summary>
        /// <returns></returns>
        public override int GetAvg()
        {
            var data = GetData();
            if (data.Count < 2)
                return default;
            var firstItem = data[0];
            var lastItem = data[data.Count - 1];
            var valueDiff = lastItem.Value - firstItem.Value;
            if (valueDiff == 0)
                return 0;
            var timeDiff = (decimal)(lastItem.AddedAt - firstItem.AddedAt).TotalSeconds;
            return (int)decimal.Divide(valueDiff, timeDiff);
        }
    }
}