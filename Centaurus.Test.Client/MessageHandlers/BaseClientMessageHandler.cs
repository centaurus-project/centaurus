using Centaurus.Domain;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test.Client
{
    public abstract class BaseClientMessageHandler
    {
        public abstract MessageTypes SupportedMessageType { get; }
        public abstract ConnectionState[] ValidConnectionStates { get; }

        public virtual Task Validate(UserWebSocketConnection connection, MessageEnvelope envelope)
        {
            if (ValidConnectionStates != null
                && ValidConnectionStates.Length > 0
                && Array.IndexOf(ValidConnectionStates, connection.ConnectionState) < 0)
                throw new InvalidStateException(
                    SupportedMessageType.ToString(),
                    connection.ConnectionState.ToString(),
                    ValidConnectionStates.Select(s => s.ToString()).ToArray());

            //do we need to check all signatures or only the first?
            if (!envelope.AreSignaturesValid())
                throw new UnauthorizedException();

            return Task.CompletedTask;
        }

        public abstract Task HandleMessage(UserWebSocketConnection connection, MessageEnvelope messageEnvelope);
    }
}
