using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    [TestFixture]
    public class WebSocketExtensionTests
    {
        [Test]
        public async Task GetInputStreamReaderTest()
        {
            var res = await new FakeWebSocket(Enumerable.Repeat((byte)1, 100).ToArray()).GetInputStreamReader();
            Assert.IsTrue(res.ToArray().All(v => v == 1));
            res = await new FakeWebSocket(Enumerable.Repeat((byte)1, 1000).ToArray()).GetInputStreamReader();
            Assert.IsTrue(res.ToArray().All(v => v == 1));
            Assert.ThrowsAsync<OutOfMemoryException>(async () => await new FakeWebSocket(Enumerable.Repeat((byte)1, 20000).ToArray()).GetInputStreamReader());
        }
    }

    class FakeWebSocket : WebSocket
    {
        public FakeWebSocket(byte[] payload)
        {
            this.payload = payload;
        }

        private readonly byte[] payload;
        private int payloadCursor = 0;
        private int remainingPayload { get { return payload.Length - payloadCursor; } }

        public bool IsAborted { get; private set; }

        public override WebSocketCloseStatus? CloseStatus => throw new NotImplementedException();

        public override string CloseStatusDescription => throw new NotImplementedException();

        public override WebSocketState State => WebSocketState.Open;

        public override string SubProtocol => throw new NotImplementedException();

        public override void Abort()
        {
            IsAborted = true;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
        }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            var bytesToWrite = Math.Min(buffer.Count, remainingPayload);
            if (bytesToWrite == 0) return new WebSocketReceiveResult(0, WebSocketMessageType.Binary, true);
            Buffer.BlockCopy(payload, payloadCursor, buffer.Array, buffer.Offset, bytesToWrite);
            payloadCursor += bytesToWrite;
            return new WebSocketReceiveResult(bytesToWrite, WebSocketMessageType.Binary, payloadCursor == payload.Length);
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
