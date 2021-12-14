using Centaurus.Domain.Quanta.Sync;
using System;
using System.Collections.Generic;

namespace Centaurus.Domain
{
    internal partial class SyncQuantaDataWorker
    {
        internal class CursorGroup
        {
            public CursorGroup(SyncCursorType cursorType, ulong batchId)
            {
                CursorType = cursorType;
                BatchId = batchId;
            }

            public SyncCursorType CursorType { get; }

            public ulong BatchId { get; }

            public DateTime LastUpdate { get; set; }

            public List<NodeCursorData> Nodes { get; } = new List<NodeCursorData>();
        }

        internal class NodeCursorData
        {
            public NodeCursorData(RemoteNode node, DateTime timeToken, ulong cursor)
            {
                Node = node;
                TimeToken = timeToken;
                Cursor = cursor;
            }

            public RemoteNode Node { get; }

            public DateTime TimeToken { get; }

            public ulong Cursor { get; }
        }
    }
}