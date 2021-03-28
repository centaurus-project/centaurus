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
        public const int ChunkSize = 1024;

        public static async Task<WebSocketMessageType> GetWebsocketBuffer(this WebSocket webSocket, XdrBufferFactory.RentedBuffer messageBuffer, CancellationToken cancellationToken)
        {
            var length = 0;
            var messageType = default(WebSocketMessageType);
            while (true)
            {
                if (length + ChunkSize > messageBuffer.Capacity) 
                    throw new Exception("Too large message"); //TODO: handle it upstream
                var chunk = messageBuffer.AsSegment(length, ChunkSize);
                var result = await webSocket.ReceiveAsync(chunk, cancellationToken);
                length += result.Count;
                if (result.EndOfMessage)
                {
                    messageType = result.MessageType;
                    break;
                }
            }
            messageBuffer.Resize(length);
            return messageType;
        }
    }
}
