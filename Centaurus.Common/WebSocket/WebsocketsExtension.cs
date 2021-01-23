using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Centaurus.Xdr;

namespace Centaurus
{

    public static class WebSocketExtension
    {
        const int chunkSize = 512;
        const int maxMessageSize = 20480;

        private static readonly ArrayPool<byte> bufferPool = ArrayPool<byte>.Create();

        public static async Task<AssembledWebSoketBuffer> GetWebsocketBuffer(this WebSocket webSocket, CancellationToken cancellationToken)
        {
            var length = 0;

            var messageBuffer = bufferPool.Rent(maxMessageSize);
            do
            {
                if (length + chunkSize > maxMessageSize) throw new Exception("Too large message"); //TODO: handle it upstream
                var chunk = new ArraySegment<byte>(messageBuffer, length, chunkSize);
                var result = await webSocket.ReceiveAsync(chunk, cancellationToken);
                length += result.Count;
                if (result.EndOfMessage) break;
                if (result.CloseStatus.HasValue)
                    throw new ConnectionCloseException(result.CloseStatus.Value, result.CloseStatusDescription);
            } while (true);

            return new AssembledWebSoketBuffer(messageBuffer, length);
        }

        public class AssembledWebSoketBuffer : IDisposable
        {
            public AssembledWebSoketBuffer(byte[] messageBuffer, int length)
            {
                Buffer = messageBuffer;
                Length = length;
            }

            private readonly byte[] Buffer;

            public readonly int Length;

            public void Dispose()
            {
                bufferPool.Return(Buffer);
            }

            public XdrReader CreateReader()
            {
                return new XdrBufferReader(Buffer, Length);
            }

            public ReadOnlySpan<byte> AsSpan()
            {
                return Buffer.AsSpan(0, Length);
            }
        }
    }
}
