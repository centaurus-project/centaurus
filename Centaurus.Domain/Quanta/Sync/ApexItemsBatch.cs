using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain.Quanta.Sync
{
    public partial class ApexItemsBatch<T>: ContextualBase
        where T : IApex
    {
        public ApexItemsBatch(ExecutionContext context, ulong start, int size, int portionSize, List<T> initData)
            :base(context)
        {
            Start = start;
            Size = size;
            PortionSize = portionSize;
            if (initData == null)
                throw new ArgumentNullException(nameof(initData));
            if (initData.Count > size)
                throw new ArgumentException("Data size is greater than batch size", nameof(initData));
            data = initData;
            data.Capacity = size;

            if (data.Count > 0)
                LastApex = data.LastOrDefault()?.Apex ?? 0; //last item could be null if it's first batch and no quanta were handled
            else
                LastApex = (start == 0 ? 0 : start - 1);

            StartWorker();
        }

        /// <summary>
        /// Returns range of items
        /// </summary>
        /// <param name="from"></param>
        /// <param name="limit"></param>
        /// <param name="inclusiveFrom"></param>
        /// <returns></returns>
        public List<T> GetItems(ulong from, int limit, bool inclusiveFrom = false)
        {
            if (!inclusiveFrom || from == 0) //we don't have 0 apex, so force exclusive for 0
                from = from + 1;
            var skip = from - Start;

            var result = data
                .Skip((int)skip)
                .Take(limit).ToList();

            //last item can be null, if inserting is in progress
            if (result.Count > 0 && result[result.Count - 1] == null)
                result.RemoveAt(result.Count - 1);
            return result;
        }

        public ulong Start { get; }

        public ulong LastApex { get; private set; }

        public int Size { get; }

        public int PortionSize { get; }

        public bool IsFulfilled => data.Count == Size;

        public void Add(ulong apex, T item)
        {
            if (apex == LastApex + 1 || apex == Start)
                AddToData(apex, item);
            else
            {
                lock (outrunSyncRoot)
                    outrunData.Add(apex, item);
            }
        }

        private object outrunSyncRoot = new { };
        private SortedDictionary<ulong, T> outrunData = new SortedDictionary<ulong, T>();
        private List<T> data;

        private void StartWorker()
        {
            Task.Factory.StartNew(() =>
            {
                //finish worker after batch is fulfilled
                while (data.Count != Size)
                    if (!TryProcessFirstOutrunItem())
                        Thread.Sleep(20);
            }, TaskCreationOptions.LongRunning);
        }

        private bool TryProcessFirstOutrunItem()
        {
            lock (outrunSyncRoot)
            {
                var nextItem = LastApex + 1;
                if (outrunData.TryGetValue(nextItem, out var item))
                {
                    AddToData(nextItem, item);
                    if (!outrunData.Remove(nextItem))
                    {
                        Console.WriteLine("Unable to remove.");
                    }
                    return true;
                }
            }
            return false;
        }

        private void AddToData(ulong apex, T item)
        {
            data.Add(item);
            LastApex = apex;
        }

        private Dictionary<ulong, ApexItemsBatchPortion> portions = new Dictionary<ulong, ApexItemsBatchPortion>();

        public SyncPortion GetData(ulong apex, bool force)
        {
            var portionStart = apex - (apex % (ulong)PortionSize);
            if (!portions.TryGetValue(portionStart, out var portion))
            {
                portion = new ApexItemsBatchPortion(portionStart, PortionSize, this);
                portions.Add(portionStart, portion);
            }
            return portion.GetBatch(force);
        }
    }
}