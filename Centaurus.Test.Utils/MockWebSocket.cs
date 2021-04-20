using Centaurus.Xdr;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Centaurus.Xdr.XdrBufferFactory;

namespace Centaurus.Test
{
    public class MockWebSocket : WebSocket
    {
        WebSocketCloseStatus? closeStatus;
        public override WebSocketCloseStatus? CloseStatus => closeStatus;

        string closeStatusDescription = null;
        public override string CloseStatusDescription => closeStatusDescription;

        WebSocketState state;
        public override WebSocketState State => state;

        string subProtocol;

        public override string SubProtocol => subProtocol;

        public override void Abort()
        {
            state = WebSocketState.Aborted;
        }

        public override Task CloseAsync(WebSocketCloseStatus _closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            if (closeStatus == null)
            {
                closeStatus = _closeStatus;
                closeStatusDescription = statusDescription;
                state = WebSocketState.Closed;
            }
            return Task.CompletedTask;
        }

        public override async Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            await SendAsync(new byte[] { }, WebSocketMessageType.Close, true, CancellationToken.None);
        }

        private BlockingCollection<TestMessage> pendingMessages = new BlockingCollection<TestMessage>();
        TestMessage currentMessage;
        SemaphoreSlim receivingSemaphore = new SemaphoreSlim(1);
        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            await receivingSemaphore.WaitAsync();
            try
            {
                if (currentMessage == null)
                    currentMessage = pendingMessages.Take();

                var startAt = currentMessage.ReadenDataLength;
                var length = Math.Min(currentMessage.Data.Length - currentMessage.ReadenDataLength, buffer.Count);

                currentMessage.Data.AsSegment(startAt, length).CopyTo(buffer);
                currentMessage.ReadenDataLength += length;

                var isEnd = currentMessage.IsEnd && currentMessage.ReadenDataLength == currentMessage.Data.Length;
                var messageType = currentMessage.Type;
                if (isEnd)
                {
                    currentMessage.Dispose();
                    currentMessage = null;
                }
                else
                { }

                return new WebSocketReceiveResult(length, messageType, isEnd);

            }
            finally
            {
                receivingSemaphore.Release();
            }
        }

        SemaphoreSlim sendingSemaphore = new SemaphoreSlim(1);

        private BlockingCollection<TestMessage> secondPartyPendingMessages;

        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            await sendingSemaphore.WaitAsync();
            try
            {
                if (!endOfMessage)
                    throw new InvalidOperationException("Only completed messages are supported.");
                var message = XdrBufferFactory.Rent(buffer.Count);
                var segment = message.AsSegment(0, buffer.Count);
                buffer.CopyTo(segment);
                message.Resize(buffer.Count);
                secondPartyPendingMessages.Add(new TestMessage(message, messageType, endOfMessage));
            }
            finally
            {
                sendingSemaphore.Release();
            }
        }

        private void AssignMessagePool(BlockingCollection<TestMessage> pendingMessages)
        {
            secondPartyPendingMessages = pendingMessages ?? throw new ArgumentNullException(nameof(pendingMessages));
        }

        public void Connect(MockWebSocket webSocket)
        {
            webSocket.AssignMessagePool(pendingMessages);
            
            state = WebSocketState.Open;
        }

        public override void Dispose()
        {
            pendingMessages?.Dispose();
            pendingMessages = null;

            receivingSemaphore?.Dispose();
            receivingSemaphore = null;

            sendingSemaphore?.Dispose();
            sendingSemaphore = null;
        }
    }

    public class TestMessage: IDisposable
    {
        public TestMessage(RentedBuffer data, WebSocketMessageType type, bool isEnd)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Type = type;
            IsEnd = isEnd;
        }

        public RentedBuffer Data { get; }

        public WebSocketMessageType Type { get; }

        public int ReadenDataLength { get; set; }

        public bool IsEnd { get; set; }

        public void Dispose()
        {
            Data.Dispose();
        }
    }
}
