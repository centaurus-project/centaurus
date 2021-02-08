﻿using System;
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

        public static async Task<XdrBufferFactory.RentedBuffer> GetWebsocketBuffer(this WebSocket webSocket, int maxMessageSize, CancellationToken cancellationToken)
        {
            var length = 0;

            var messageBuffer = XdrBufferFactory.Rent(maxMessageSize);
            do
            {
                if (length + ChunkSize > maxMessageSize) throw new Exception("Too large message"); //TODO: handle it upstream
                var chunk = messageBuffer.AsSegment(length, ChunkSize);
                var result = await webSocket.ReceiveAsync(chunk, cancellationToken);
                length += result.Count;
                if (result.EndOfMessage) break;
                if (result.CloseStatus.HasValue)
                    throw new ConnectionCloseException(result.CloseStatus.Value, result.CloseStatusDescription);
            } while (true);
            messageBuffer.Resize(length);
            return messageBuffer;
        }
    }
}
