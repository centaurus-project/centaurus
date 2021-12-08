using Centaurus.Domain.StateManagers;
using System;

namespace Centaurus.Domain.Quanta.Sync
{
    public enum SyncCursorType
    {
        Quanta = 0,
        Signatures = 1
    }

    internal class SyncCursorUpdate
    {
        public SyncCursorUpdate(DateTime timeToken, ulong newCursor, SyncCursorType cursorType)
        {
            NewCursor = newCursor;
            CursorType = cursorType;
            TimeToken = timeToken;
        }

        public ulong NewCursor { get; }

        public SyncCursorType CursorType { get; }

        public DateTime TimeToken { get; }
    }

    internal class NodeSyncCursorUpdate
    {
        public NodeSyncCursorUpdate(RemoteNode node, SyncCursorUpdate syncCursorUpdate)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            CursorUpdate = syncCursorUpdate ?? throw new ArgumentNullException(nameof(syncCursorUpdate));
        }

        public RemoteNode Node { get; }

        public SyncCursorUpdate CursorUpdate { get; set; }
    }
}
