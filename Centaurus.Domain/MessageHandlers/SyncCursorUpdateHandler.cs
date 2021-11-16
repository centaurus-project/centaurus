using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class SyncCursorUpdateHandler : MessageHandlerBase<IncomingAuditorConnection>
    {
        public SyncCursorUpdateHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(SyncCursorUpdate).Name;

        public override bool IsAuditorOnly { get; } = true;

        public override ConnectionState[] ValidConnectionStates { get; } =
            new ConnectionState[] {
                ConnectionState.Ready
            };

        public override Task HandleMessage(IncomingAuditorConnection connection, IncomingMessage message)
        {
            var batchRequest = (SyncCursorUpdate)message.Envelope.Message;
            var quantaCursor = batchRequest.QuantaCursor == ulong.MaxValue ? null : (ulong?)batchRequest.QuantaCursor;
            var signaturesCursor = batchRequest.SignaturesCursor == ulong.MaxValue ? null : (ulong?)batchRequest.SignaturesCursor;
            connection.SetSyncCursor(quantaCursor, signaturesCursor);
            return Task.CompletedTask;
        }
    }
}