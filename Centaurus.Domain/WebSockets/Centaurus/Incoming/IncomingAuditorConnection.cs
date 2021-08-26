using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using System.Net.WebSockets;

namespace Centaurus
{
    public class IncomingAuditorConnection : IncomingConnectionBase, IAuditorConnection
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        public IncomingAuditorConnection(ExecutionContext context, KeyPair keyPair, WebSocket webSocket, string ip)
            : base(context, keyPair, webSocket, ip)
        {
            quantumWorker = new QuantumSyncWorker(Context, this);
            SendHandshake();
        }

        const int AuditorBufferSize = 50 * 1024 * 1024;

        protected override int inBufferSize => AuditorBufferSize;

        protected override int outBufferSize => AuditorBufferSize;

        private QuantumSyncWorker quantumWorker;

        public ulong CurrentCursor => quantumWorker.CurrentQuantaCursor;

        public void SetSyncCursor(ulong newQuantumCursor, ulong newResultCursor)
        {
            var resultCursor = newResultCursor == ulong.MaxValue ? (ulong?)null : newResultCursor;

            logger.Trace($"Connection {PubKeyAddress}, apex cursor reset requested. New quantum cursor {newQuantumCursor}, new result cursor {(resultCursor.HasValue ? newResultCursor.ToString() : "null")}");

            //set new quantum and result cursors
            quantumWorker.SetCursors(newQuantumCursor, resultCursor);
            logger.Trace($"Connection {PubKeyAddress}, cursors reseted.");
        }

        public override void Dispose()
        {
            base.Dispose();
            quantumWorker.Dispose();
        }
    }
}