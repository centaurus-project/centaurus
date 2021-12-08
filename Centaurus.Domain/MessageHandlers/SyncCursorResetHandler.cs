using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Domain.Quanta.Sync;
using Centaurus.Models;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    internal class SyncCursorResetHandler : MessageHandlerBase<IncomingNodeConnection>
    {
        public SyncCursorResetHandler(ExecutionContext context)
            : base(context)
        {
        }

        public override string SupportedMessageType { get; } = typeof(SyncCursorReset).Name;

        public override bool IsAuditorOnly { get; } = true;

        public override ConnectionState[] ValidConnectionStates { get; } =
            new ConnectionState[] {
                ConnectionState.Ready
            };

        public override Task HandleMessage(IncomingNodeConnection connection, IncomingMessage message)
        {
            var cursorResetRequest = (SyncCursorReset)message.Envelope.Message;

            foreach (var cursor in cursorResetRequest.Cursors)
            {
                var cursorType = cursor.Type.ToDomainCursorType();
                if (cursor.DisableSync)
                    connection.Node.DisableSync(cursorType);
                else
                    connection.Node.SetCursor(cursorType, default, cursor.Cursor, true);
            }

            return Task.CompletedTask;
        }
    }
}