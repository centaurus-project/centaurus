using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using RocksDbSharp;

namespace Centaurus.PersistentStorage
{
    public class StorageIterator<T> : IDisposable, IEnumerable<T> where T : IPersistentModel, new()
    {
        internal StorageIterator(Iterator iterator)
        {
            this.iterator = iterator;
        }

        private readonly Iterator iterator;

        private Func<byte[], bool> isKeyWithinBoundaries;

        public bool IsReversed { get; private set; }

        private T ParseCurrent()
        {
            return IPersistentModel.Deserialize<T>(iterator.Key(), iterator.Value());
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
                if (isKeyWithinBoundaries != null && !isKeyWithinBoundaries(iterator.Key())) break;
                yield return ParseCurrent();
                Next();
            }
        }

        public T First()
        {
            var res = ParseCurrent();
            Next();
            return res;
        }

        public StorageIterator<T> Reverse()
        {
            if (IsReversed)
            {
                IsReversed = false;
                iterator.SeekToFirst();
            }
            else
            {
                IsReversed = true;
                iterator.SeekToLast();
            }
            return this;
        }

        public StorageIterator<T> SetBoundaryCheck(Func<byte[], bool> checkKeyIsWithinBoundaries)
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
