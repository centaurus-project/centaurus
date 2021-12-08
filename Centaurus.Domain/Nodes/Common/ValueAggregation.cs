using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain.StateManagers
{
    internal abstract class ValueAggregation<T>
    {
        protected ValueAggregation(int maxItems)
        {
            if (maxItems < 2)
                throw new ArgumentException("Data must contain at least two items to be able to calculate avg value.", nameof(maxItems));
        }

        private List<Item<T>> data = new List<Item<T>>();

        private object syncRoot = new { };

        public void AddValue(DateTime dateTime, T value)
        {
            lock (syncRoot)
            {
                data.Add(new Item<T> { AddedAt = dateTime, Value = value });
                LastValue = value;
                if (data.Count > 20) //remove old data
                    data.RemoveAt(0);
            }
        }

        public T LastValue { get; private set; }

        public void Clear()
        {
            lock (syncRoot)
                data.Clear();
        }

        public abstract int GetAvg();

        protected List<Item<T>> GetData()
        {
            lock (syncRoot)
                return data.ToList();
        }

        protected struct Item<T>
        {
            public DateTime AddedAt { get; set; }

            public T Value { get; set; }
        }
    }
}
