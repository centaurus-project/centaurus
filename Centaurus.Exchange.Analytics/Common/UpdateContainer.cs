using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Exchange.Analytics
{
    //TODO: add comparer for complex keys
    public class UpdateContainer<TKey, TValue>
    {
        public UpdateContainer()
        {
            updates = new Dictionary<TKey, TValue>();
        }

        private Dictionary<TKey, TValue> updates;

        public void AddUpdate(TKey key, TValue value)
        {
            lock (this)
            {
                updates[key] = value;
            }
        }

        public List<TValue> PullUpdates()
        {
            lock(this)
            {
                var values = updates.Values.ToList();
                updates.Clear();
                return values;
            }
        }
    }
}
