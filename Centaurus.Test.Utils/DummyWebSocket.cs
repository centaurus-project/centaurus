using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class DummyWebSocket : WebSocket
    {
        public DummyWebSocket()
        {

        }

        public DummyWebSocket(byte[] payload)
        {
            this.payload = payload;
        }

        private readonly byte[] payload = new byte[] { };
        private int payloadCursor = 0;
        private int remainingPayload { get { return payload.Length - payloadCursor; } }

        public bool IsAborted { get; private set; }

        private WebSocketCloseStatus? closeStatus;
        public override WebSocketCloseStatus? CloseStatus => closeStatus;

        private string closeStatusDescription;
        public override string CloseStatusDescription => closeStatusDescription;

        public override WebSocketState State => WebSocketState.Open;

        public override string SubProtocol => throw new NotImplementedException();

        public override void Abort()
        {
            IsAborted = true;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            this.closeStatus = closeStatus;
            this.closeStatusDescription = statusDescription;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            this.closeStatus = closeStatus;
            this.closeStatusDescription = statusDescription;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            var bytesToWrite = Math.Min(buffer.Count, remainingPayload);
            if (bytesToWrite == 0) return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Binary, true));
            Buffer.BlockCopy(payload, payloadCursor, buffer.Array, buffer.Offset, bytesToWrite);
            payloadCursor += bytesToWrite;
            return Task.FromResult(new WebSocketReceiveResult(bytesToWrite, WebSocketMessageType.Binary, payloadCursor == payload.Length));
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
