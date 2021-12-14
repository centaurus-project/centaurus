using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal abstract class MessageHandlerBase : ContextualBase
    {
        protected MessageHandlerBase(ExecutionContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Handler supported message types
        /// </summary>
        public abstract string SupportedMessageType { get; }

        /// <summary>
        /// Only messages from the authenticated connections are allowed.
        /// </summary>
        public virtual bool IsAuthenticatedOnly { get; }

        /// <summary>
        /// If set to true, than messages will be handled only if the other side is an auditor. 
        /// </summary>
        public virtual bool IsAuditorOnly { get; }

        /// <summary>
        /// Validates authentication, connection state and message.
        /// </summary>
        /// <param name="envelope">The current message envelope</param>
        public virtual Task Validate(ConnectionBase connection, IncomingMessage message)
        {

            if (IsAuthenticatedOnly && !connection.IsAuthenticated)
                throw new UnauthorizedException();

            if (!message.Envelope.IsSignatureValid(connection.PubKey, message.MessageHash, connection.IsAuditor && connection.IsAuthenticated))
            {
                throw new UnauthorizedException();
            }

            ValidateAuditor(connection);
            ValidateClient(connection);

            return Task.CompletedTask;
        }

        private void ValidateAuditor(ConnectionBase connection)
        {
            if (IsAuditorOnly && !connection.IsAuditor)
                throw new UnauthorizedException();
        }

        private void ValidateClient(ConnectionBase connection)
        {
            if (connection is IncomingClientConnection clientConnection //check requests count only for a client connection 
                && clientConnection.IsAuthenticated //increment requests count only for validated connection
                && !clientConnection.Account.RequestCounter.IncRequestCount(DateTime.UtcNow.Ticks, out var error))
                throw new TooManyRequestsException(error);
        }

        /// <summary>
        /// Handles message
        /// </summary>
        /// <param name="messageEnvelope">The current message envelope</param>
        /// <returns>Handle result</returns>
        public abstract Task HandleMessage(ConnectionBase connection, IncomingMessage message);
    }

    internal abstract class MessageHandlerBase<T> : MessageHandlerBase
        where T: ConnectionBase
    {
        protected MessageHandlerBase(ExecutionContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Validates authentication, connection state and message.
        /// </summary>
        /// <param name="envelope">The current message envelope</param>
        public virtual Task Validate(T connection, IncomingMessage message)
        {
            return base.Validate(connection, message);
        }

        public override Task Validate(ConnectionBase connection, IncomingMessage message)
        {
            return Validate(GetTypedConnection(connection), message);
        }

        private T GetTypedConnection(ConnectionBase connection)
        {
            if (!(connection is T typedConnection))
                throw new UnauthorizedException($"Invalid connection type. Only {nameof(T)} connections are supported.");
            return typedConnection;
        }

        /// <summary>
        /// Handles message
        /// </summary>
        /// <param name="messageEnvelope">The current message envelope</param>
        /// <returns>Handle result</returns>
        public abstract Task HandleMessage(T connection, IncomingMessage message);

        public override Task HandleMessage(ConnectionBase connection, IncomingMessage message)
        {
            return HandleMessage(GetTypedConnection(connection), message);
        }
    }

    public class IncomingMessage
    {

        public IncomingMessage(MessageEnvelopeBase envelope, byte[] messageHash)
        {
            Envelope = envelope ?? throw new ArgumentNullException(nameof(Envelope));
            MessageHash = messageHash ?? throw new ArgumentNullException(nameof(messageHash));
        }

        public MessageEnvelopeBase Envelope { get; }

        public byte[] MessageHash { get; }
    }
}
