using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
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

        private static async Task<(byte[] buffer, int length)> GetByteArray(WebSocket webSocket)
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
            return (buffer, length);
        }

        public static async Task<XdrReader> GetInputStreamReader(this WebSocket webSocket)
        {
            var res = await GetByteArray(webSocket);
            return new XdrReader(res.buffer, res.length);
        }

        public static async Task<string> GetString(this WebSocket webSocket)
        {
            var res = await GetByteArray(webSocket);
            return Encoding.UTF8.GetString(res.buffer, 0, res.length);
        }
    }
}
