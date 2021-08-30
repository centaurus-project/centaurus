using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class QuantaThrottlingManager
    {
        private List<int> batchQueueLengthHistory = new List<int>();

        public int SleepTime { get; private set; }

        public int MaxItemsPerSecond { get; private set; }

        public bool IsThrottlingEnabled => batchQueueLengthHistory.Count > 0;

        public void SetBatchQueueLength(int batchQueueLength)
        {
            lock (batchQueueLengthHistory)
            {
                //TODO: refactor it
                //don't need to start throttling yet, batch queue length is less than maximum allowed
                if (batchQueueLengthHistory.Count == 0 && batchQueueLength < maxAllowedBatchLength)
                    return;

                if (batchQueueLength <= 3) //stop throttling if the queue is 3 or less
                {
                    Reset();
                    return;
                }

                batchQueueLengthHistory.Add(batchQueueLength);
                if (batchQueueLengthHistory.Count == 1 
                    || (batchQueueLengthHistory.Count >= 3 && batchQueueLengthHistory.First() - batchQueueLengthHistory.Last() < 2))
                    Throttle();
            }
        }

        private int maxAllowedBatchLength = 10;

        private void Reset()
        {
            MaxItemsPerSecond = 0;
            SleepTime = 0;
            batchQueueLengthHistory.Clear();
        }

        private void Throttle()
        {
            if (MaxItemsPerSecond == 1)
                return;

            MaxItemsPerSecond = MaxItemsPerSecond != 0 ? MaxItemsPerSecond / 2 : 1000; //start with max 1000 items per second
            SleepTime = 1000 / MaxItemsPerSecond; //1000ms divided by max quanta per second
        }

        public static QuantaThrottlingManager Current { get; } = new QuantaThrottlingManager();
    }
}
