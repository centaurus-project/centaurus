using RocksDbSharp;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Centaurus.PersistentStorage
{
    public class StorageIterator<T> : IDisposable, IEnumerable<T> where T : IPersistentModel, new()
    {
        internal StorageIterator(Iterator iterator, QueryOrder queryOrder = QueryOrder.Asc)
        {
            this.iterator = iterator;
            IsReversed = queryOrder == QueryOrder.Desc;
            if (IsReversed)
            {
                iterator.SeekToLast();
            }
            else
            {
                iterator.SeekToFirst();
            }
        }

        private readonly Iterator iterator;

        public readonly bool IsReversed;

        private Func<byte[], bool> isKeyWithinBoundaries;

        private byte[] toBoundary;


        private T ParseCurrent(byte[] key)
        {
            return IPersistentModel.Deserialize<T>(key, iterator.Value());
        }

        private void Next()
        {
            if (IsReversed)
            {
                iterator.Prev();
            }
            else
            {
                iterator.Next();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            while (iterator.Valid())
            {
                var key = iterator.Key();
                if (toBoundary != null)
                {
                    if (IsReversed)
                    {
                        if (key.AsSpan().SequenceCompareTo(toBoundary) < 0) break;
                    }
                    else
                    {
                        if (key.AsSpan().SequenceCompareTo(toBoundary) > 0) break;
                    }
                }
                yield return ParseCurrent(key);
                Next();
            }
        }

        public T First()
        {
            var res = ParseCurrent(iterator.Key());
            Next();
            return res;
        }

        private StorageIterator<T> SetBoundaryCheck(Func<byte[], bool> checkKeyIsWithinBoundaries)
        {
            isKeyWithinBoundaries = checkKeyIsWithinBoundaries;
            return this;
        }

        public List<T> Take(int count)
        {
            var res = new List<T>(count);
            foreach (var obj in this)
            {
                if (obj == null) break;
                res.Add(obj);
                if (res.Count >= count) break;
            }
            return res;
        }

        internal StorageIterator<T> To(byte[] toBoundary)
        {
            this.toBoundary = toBoundary;
            return this;
        }

        internal StorageIterator<T> From(byte[] from)
        {
            iterator.Seek(from);
            if (IsReversed || iterator.Key().AsSpan().SequenceEqual(from))
            {
                if (iterator.Valid())
                    Next();
            }
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            iterator?.Dispose();
        }
    }
}
