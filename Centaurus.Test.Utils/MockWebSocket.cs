using Centaurus.Xdr;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        public override string SubProtocol => null;

        public override void Abort()
        {
            state = WebSocketState.Aborted;
            secondPartyWebsocket?.SetClosed();
        }

        private void SetClosed()
        {
            CloseAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
        }

        public override Task CloseAsync(WebSocketCloseStatus _closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            if (closeStatus == null)
            {
                closeStatus = _closeStatus;
                closeStatusDescription = statusDescription;
                state = WebSocketState.Closed;
                listenToken?.Cancel();
                secondPartyWebsocket.SetClosed();
            }
            return Task.CompletedTask;
        }

        public override async Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            await SendAsync(new byte[] { }, WebSocketMessageType.Close, true, CancellationToken.None);
        }

        private BlockingCollection<TestMessage> pendingMessages = new BlockingCollection<TestMessage>();
        TestMessage currentMessage;
        object receivingSemaphore = new { };
        CancellationTokenSource listenToken = new CancellationTokenSource();
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            lock (receivingSemaphore)
            {
                if (currentMessage == null)
                    currentMessage = pendingMessages.Take(listenToken.Token);

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

                return Task.FromResult(new WebSocketReceiveResult(length, messageType, isEnd));
            }
        }

        object sendingSemaphore = new { };

        private BlockingCollection<TestMessage> secondPartyPendingMessages;
        private MockWebSocket secondPartyWebsocket;

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            lock (sendingSemaphore)
            {
                if (!endOfMessage)
                    throw new InvalidOperationException("Only completed messages are supported.");
                var message = XdrBufferFactory.Rent(buffer.Count);
                var segment = message.AsSegment(0, buffer.Count);
                buffer.CopyTo(segment);
                message.Resize(buffer.Count);
                secondPartyPendingMessages.Add(new TestMessage(message, messageType, endOfMessage));
            }
            return Task.CompletedTask;
        }

        private void AssignMessagePool(BlockingCollection<TestMessage> pendingMessages)
        {
            secondPartyPendingMessages = pendingMessages ?? throw new ArgumentNullException(nameof(pendingMessages));
        }

        public void Connect(MockWebSocket webSocket)
        {
            secondPartyWebsocket = webSocket;
            webSocket.AssignMessagePool(pendingMessages);
            state = WebSocketState.Open;
        }

        public override void Dispose()
        {
            pendingMessages?.Dispose();
            pendingMessages = null;
        }
    }

    public class TestMessage : IDisposable
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
