using Centaurus.Domain.Quanta.Sync;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain.Nodes.Common
{
    internal class SyncCursorCollection
    {
        public RemoteNodeCursor Get(SyncCursorType cursorType, bool createIfNotExist = false)
        {
            var cursor = default(RemoteNodeCursor);
            lock (syncRoot)
                if (!cursors.TryGetValue(cursorType, out cursor) && createIfNotExist)
                {
                    cursor = new RemoteNodeCursor(cursorType);
                    cursors.Add(cursorType, cursor);
                }
            return cursor;
        }

        public void Remove(SyncCursorType cursorType)
        {
            lock (syncRoot)
                cursors.Remove(cursorType);
        }

        public List<RemoteNodeCursor> GetActiveCursors()
        {
            lock (syncRoot)
                return cursors.Values.Where(c => c.IsSyncEnabled).ToList();
        }

        public void Clear()
        {
            lock (syncRoot)
                cursors.Clear();
        }

        private object syncRoot = new { };
        private Dictionary<SyncCursorType, RemoteNodeCursor> cursors = new Dictionary<SyncCursorType, RemoteNodeCursor>();
    }
}
