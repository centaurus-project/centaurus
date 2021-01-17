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

        //TODO: validate msg size
        public static async Task<byte[]> GetInputByteArray(this WebSocket webSocket, CancellationToken cancellationToken)
        {
            var buffer = WebSocket.CreateClientBuffer(chunkSize, chunkSize);
            using (var ms = new MemoryStream())
            {
                var result = default(WebSocketReceiveResult);
                do
                {
                    result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                    if (result.CloseStatus.HasValue)
                        throw new ConnectionCloseException(result.CloseStatus.Value, result.CloseStatusDescription);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                    //if (checkLength && ms.Length > maxMessageSize) 
                    //    throw new OutOfMemoryException("Suspiciously large message");
                } while (!result.EndOfMessage);
                return ms.ToArray();
            }
        }

        public static async Task<XdrReader> GetInputStreamReader(this WebSocket webSocket, CancellationToken cancellationToken)
        {
            var res = await GetInputByteArray(webSocket, cancellationToken);
            return new XdrReader(res, res.Length);
        }
    }
}
