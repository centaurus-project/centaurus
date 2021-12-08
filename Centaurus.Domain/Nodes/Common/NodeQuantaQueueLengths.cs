using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain.StateManagers
{
    internal class NodeQuantaQueueLengths : ValueAggregation<int>
    {
        public NodeQuantaQueueLengths()
            :base(20)
        {

        }

        public override int GetAvg()
        {
            var data = GetData();
            if (data.Count < 1)
                return 0;

            return (int)data.Average(d => d.Value);
        }
    }
}
