using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    public class ApexItemsBatch<T>
        where T : IApex
    {
        public ApexItemsBatch(ulong start, int size, List<T> initData = null)
        {
            Start = start;
            Size = size;
            if (initData.Count > size)
                throw new ArgumentException("Data size is greater than batch size", nameof(initData));
            data = initData ?? new List<T>();
            data.Capacity = size;

            if (initData.Count > 0)
                LastApex = initData.Last().Apex;
            else
                LastApex = (start == 0 ? 0 : start - 1);

            StartWorker();
        }

        /// <summary>
        /// Returns range of items
        /// </summary>
        /// <param name="from">Exclusive from</param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public List<T> GetItems(ulong from, int limit)
        {
            from = from + 1;
            var skip = from - Start;
            limit = Math.Min((int)(LastApex - Start + skip), limit);

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
    }
}
