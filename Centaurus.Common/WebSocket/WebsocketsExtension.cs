using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Centaurus.Xdr;

namespace Centaurus
{
    public static class WebSocketExtension
    {
        const int chunkSize = 512;
        const int maxMessageSize = 10240;

        //TODO: add cancellation token

        public static async Task<XdrReader> GetInputStreamReader(this WebSocket webSocket)
        {
            var buffer = new byte[chunkSize];

            int length = 0;
            do
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, length, chunkSize), CancellationToken.None);
                if (result.CloseStatus.HasValue)
                    throw new ConnectionCloseException(result.CloseStatus.Value, result.CloseStatusDescription);
                length += result.Count;
                if (result.EndOfMessage) break;
                //resize input buffer to accommodate more data
                Array.Resize(ref buffer, buffer.Length + chunkSize);
                if (buffer.Length >= maxMessageSize) throw new OutOfMemoryException("Suspiciously large message");
                //TODO: handle cases when the payload is too large without exceptions
            } while (true);

            return new XdrReader(buffer, length);
        }

        public static async Task<byte[]> GetInputArray(this WebSocket webSocket)
        {
            var buffer = new byte[chunkSize];

            int length = 0;
            do
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, length, chunkSize), CancellationToken.None);
                if (result.CloseStatus.HasValue)
                    throw new ConnectionCloseException(result.CloseStatus.Value, result.CloseStatusDescription);
                length += result.Count;
                if (result.EndOfMessage) break;
                //resize input buffer to accommodate more data
                Array.Resize(ref buffer, buffer.Length + chunkSize);
                if (buffer.Length >= maxMessageSize) throw new OutOfMemoryException("Suspiciously large message");
                //TODO: handle cases when the payload is too large without exceptions
            } while (true);

            return buffer;
        }
    }
}
