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
        const int maxMessageSize = 10240;

        //TODO: add cancellation token

        public static async Task<byte[]> GetInputByteArray(this WebSocket webSocket)
        {
            var buffer = WebSocket.CreateClientBuffer(chunkSize, chunkSize);
            using (var ms = new MemoryStream())
            {
                var result = default(WebSocketReceiveResult);
                do
                {
                    result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.CloseStatus.HasValue)
                        throw new ConnectionCloseException(result.CloseStatus.Value, result.CloseStatusDescription);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                    if (ms.Length > maxMessageSize) 
                        throw new OutOfMemoryException("Suspiciously large message");
                } while (!result.EndOfMessage);
                return ms.ToArray();
            }
        }

        public static async Task<XdrReader> GetInputStreamReader(this WebSocket webSocket)
        {
            var res = await GetInputByteArray(webSocket);
            return new XdrReader(res, res.Length);
        }
    }
}
