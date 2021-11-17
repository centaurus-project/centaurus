using Centaurus.Domain.Quanta.Sync;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class SyncCursorUpdateExtensions
    {
        public static List<SyncCursorUpdate> ToDomainModel(this List<SyncCursor> syncCursors)
        {
            if (syncCursors == null)
                throw new ArgumentNullException(nameof(syncCursors));

            var cursors = new List<SyncCursorUpdate>();

            foreach (var cursorReset in syncCursors)
            {
                var cursorType = default(SyncCursorType);
                switch (cursorReset.Type)
                {
                    case XdrSyncCursorType.Quanta:
                        cursorType = SyncCursorType.Quanta;
                        break;
                    case XdrSyncCursorType.Signatures:
                        cursorType = SyncCursorType.Signatures;
                        break;
                    default:
                        throw new ArgumentNullException($"{cursorReset.Type} cursor type is not supported.");
                }
                var cursor = cursorReset.ClearCursor ? null : (ulong?)cursorReset.Cursor;
                cursors.Add(new SyncCursorUpdate(default(DateTime), cursor, cursorType));
            }

            return cursors;
        }
    }
}
