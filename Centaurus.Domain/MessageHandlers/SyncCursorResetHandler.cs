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
    public class SyncCursorResetHandler : MessageHandlerBase<IncomingAuditorConnection>
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

        public override Task HandleMessage(IncomingAuditorConnection connection, IncomingMessage message)
        {
            var cursorResetRequest = (SyncCursorReset)message.Envelope.Message;

            connection.SetSyncCursor(true, cursorResetRequest.SyncCursors.ToDomainModel().ToArray());
            return Task.CompletedTask;
        }
    }
}