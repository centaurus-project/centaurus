using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class MessageHandlerBase : ContextualBase
    {
        protected MessageHandlerBase(ExecutionContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Handler supported message types
        /// </summary>
        public abstract MessageTypes SupportedMessageType { get; }

        /// <summary>
        /// Only specified states are valid for the handler. Should be null or empty if any state is valid.
        /// </summary>
        public virtual ConnectionState[] ValidConnectionStates { get; }

        /// <summary>
        /// Indicates whether authorization is required for the handler.
        /// </summary>
        public virtual bool IsAuthRequired => !((ValidConnectionStates?.Length ?? 0) == 0 || ValidConnectionStates.Any(s => s == ConnectionState.Connected || s == ConnectionState.Closed));

        /// <summary>
        /// If set to true, than messages will be handled only if the other side is an auditor. 
        /// </summary>
        public virtual bool IsAuditorOnly { get; }

        /// <summary>
        /// Validates authentication, connection state and message.
        /// </summary>
        /// <param name="envelope">The current message envelope</param>
        public virtual Task Validate(BaseWebSocketConnection connection, IncomingMessage message)
        {

            if ((ValidConnectionStates?.Length ?? 0) > 0
                && Array.IndexOf(ValidConnectionStates, connection.ConnectionState) < 0)
                throw new InvalidStateException(
                    SupportedMessageType.ToString(),
                    connection.ConnectionState.ToString(),
                    ValidConnectionStates.Select(s => s.ToString()).ToArray());

            //if auth is required, then we should check that the current client public key is set, and that the envelope signatures contains it
            if (IsAuthRequired || IsAuditorOnly)
            {
                if (connection.PubKey == null || !message.Envelope.Signatures.Any(s => s.Signer != connection.PubKey))
                    throw new UnauthorizedException();

                ValidateAuditor(connection);
                ValidateClient(connection);

                if (!message.Envelope.Signatures.AreSignaturesValid(message.MessageHash))
                    throw new UnauthorizedException();
            }

            return Task.CompletedTask;
        }

        private void ValidateAuditor(BaseWebSocketConnection connection)
        {
            if (IsAuditorOnly && !connection.IsAuditor)
                throw new UnauthorizedException();
        }

        private void ValidateClient(BaseWebSocketConnection connection)
        {
            if (!(IsAuditorOnly || connection.Account.RequestCounter.IncRequestCount(DateTime.UtcNow.Ticks, out string error)))
                throw new TooManyRequestsException(error);
        }

        /// <summary>
        /// Handles message
        /// </summary>
        /// <param name="messageEnvelope">The current message envelope</param>
        /// <returns>Handle result</returns>
        public abstract Task HandleMessage(BaseWebSocketConnection connection, IncomingMessage message);
    }
    public class IncomingMessage
    {

        public IncomingMessage(MessageEnvelope envelope, byte[] messageHash)
        {
            Envelope = envelope ?? throw new ArgumentNullException(nameof(Envelope));
            MessageHash = messageHash ?? throw new ArgumentNullException(nameof(messageHash));
        }

        public MessageEnvelope Envelope { get; }

        public byte[] MessageHash { get; }
    }
}
