using System;

namespace Centaurus.Domain.Quanta.Sync
{
    public enum SyncCursorType
    {
        Quanta = 0,
        Signatures = 1
    }

    public class SyncCursorUpdate
    {
        public SyncCursorUpdate(DateTime timeToken, ulong? newCursor, SyncCursorType cursorType)
        {
            NewCursor = newCursor;
            CursorType = cursorType;
            TimeToken = timeToken;
        }

        public ulong? NewCursor { get; }

        public SyncCursorType CursorType { get; }

        public DateTime TimeToken { get; }
    }

    public class AuditorSyncCursorUpdate
    {
        public AuditorSyncCursorUpdate(IncomingAuditorConnection connection, SyncCursorUpdate syncCursorUpdate)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            CursorUpdate = syncCursorUpdate ?? throw new ArgumentNullException(nameof(syncCursorUpdate));
        }

        public IncomingAuditorConnection Connection { get; }

        public SyncCursorUpdate CursorUpdate { get; set; }
    }
}
