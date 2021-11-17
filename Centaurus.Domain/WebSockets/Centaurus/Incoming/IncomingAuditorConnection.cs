using Centaurus.Domain;
using Centaurus.Domain.Quanta.Sync;
using NLog;
using System;
using System.Net.WebSockets;
using static Centaurus.Domain.StateManager;

namespace Centaurus
{
    public class IncomingAuditorConnection : IncomingConnectionBase, IAuditorConnection
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public IncomingAuditorConnection(ExecutionContext context, KeyPair keyPair, WebSocket webSocket, string ip)
            : base(context, keyPair, webSocket, ip)
        {
            AuditorState = Context.StateManager.GetAuditorState(keyPair);
            SendHandshake();
        }

        const int AuditorBufferSize = 50 * 1024 * 1024;

        protected override int inBufferSize => AuditorBufferSize;

        protected override int outBufferSize => AuditorBufferSize;

        public AuditorState AuditorState { get; }

        private object syncRoot = new { };

        public ulong? CurrentQuantaCursor { get; private set; }
        public ulong? CurrentSignaturesCursor { get; private set; }
        public DateTime LastCursorUpdateDate { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="force">Force means that auditor requested cursor reset</param>
        /// <param name="cursorUpdates"></param>
        public void SetSyncCursor(bool force, params SyncCursorUpdate[] cursorUpdates)
        {
            //set new quantum and signature cursors
            lock (syncRoot)
            {
                if (cursorUpdates.Length == 0)
                    return;
                foreach (var cursorUpdate in cursorUpdates)
                {
                    if (!(force || cursorUpdate.TimeToken == LastCursorUpdateDate))
                        continue;
                    switch (cursorUpdate.CursorType)
                    {
                        case SyncCursorType.Quanta:
                            CurrentQuantaCursor = cursorUpdate.NewCursor;
                            break;
                        case SyncCursorType.Signatures:
                            CurrentSignaturesCursor = cursorUpdate.NewCursor;
                            break;
                        default:
                            throw new NotImplementedException($"{cursorUpdate.CursorType} cursor type is not supported.");
                    }
                }
                LastCursorUpdateDate = DateTime.UtcNow;
                if (force)
                    logger.Info($"Connection {PubKeyAddress}, cursors reset. Quanta cursor: {CurrentQuantaCursor}, signatures cursor: {CurrentSignaturesCursor}");
            }
        }

        public (ulong? quantaCursor, ulong? signaturesCursor, DateTime timeToken) GetCursors()
        {
            lock (syncRoot)
                return (CurrentQuantaCursor, CurrentSignaturesCursor, LastCursorUpdateDate);
        }
    }
}