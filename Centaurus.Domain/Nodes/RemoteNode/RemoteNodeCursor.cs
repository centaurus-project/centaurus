using Centaurus.Domain.Quanta.Sync;
using System;

namespace Centaurus.Domain
{
    internal class RemoteNodeCursor
    {
        private object syncRoot = new { };

        public RemoteNodeCursor(SyncCursorType cursorType)
        {
            CursorType = cursorType;
        }

        public SyncCursorType CursorType { get; }

        public ulong Cursor { get; private set; }

        public DateTime UpdateDate { get; private set; }

        /// <summary>
        /// Disable data sync
        /// </summary>
        public void DisableSync()
        {
            lock (syncRoot)
            {
                IsSyncEnabled = false;
                UpdateDate = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeToken">Time token to validate that node didn't requested new cursor</param>
        /// <param name="cursor">New cursor value</param>
        /// <param name="force">Set true to skip time token validation</param>
        public void SetCursor(ulong cursor, DateTime timeToken, bool force = false)
        {
            //set new cursor
            lock (syncRoot)
            {
                if (!(force || timeToken == UpdateDate))
                    return;
                Cursor = cursor;
                IsSyncEnabled = true;
                UpdateDate = DateTime.UtcNow;
            }
        }

        public bool IsSyncEnabled { get; private set; }
    }
}