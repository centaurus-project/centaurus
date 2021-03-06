﻿using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public abstract class BaseMessageHandler : ContextualBase
    {
        protected BaseMessageHandler(ExecutionContext context) 
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
        public abstract ConnectionState[] ValidConnectionStates { get; }

        /// <summary>
        /// Validates authentication, connection state and message.
        /// </summary>
        /// <param name="envelope">The current message envelope</param>
        public abstract Task Validate(BaseWebSocketConnection connection, IncomingMessage message);

        /// <summary>
        /// Handles message
        /// </summary>
        /// <param name="messageEnvelope">The current message envelope</param>
        /// <returns>Handle result</returns>
        public abstract Task HandleMessage(BaseWebSocketConnection connection, IncomingMessage message);
    }

    public abstract class BaseMessageHandler<TConnection, TContext> : BaseMessageHandler, IContextual<TContext>
        where TConnection: BaseWebSocketConnection
        where TContext: ExecutionContext
    {
        public BaseMessageHandler(TContext context)
            :base(context)
        {

        }

        public new TContext Context => (TContext)base.Context;

        /// <summary>
        /// Validates authentication, connection state and message.
        /// </summary>
        /// <param name="envelope">The current message envelope</param>
        public virtual Task Validate(TConnection connection, IncomingMessage message)
        {
            if (ValidConnectionStates != null
                && ValidConnectionStates.Length > 0
                && Array.IndexOf(ValidConnectionStates, connection.ConnectionState) < 0)
                throw new InvalidStateException(
                    SupportedMessageType.ToString(),
                    connection.ConnectionState.ToString(),
                    ValidConnectionStates.Select(s => s.ToString()).ToArray());

            if (!message.Envelope.Signatures.AreSignaturesValid(message.MessageHash))
                throw new UnauthorizedException();

            return Task.CompletedTask;
        }

        public override Task Validate(BaseWebSocketConnection connection, IncomingMessage message)
        {
            Validate((TConnection)connection, message);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles message
        /// </summary>
        /// <param name="messageEnvelope">The current message envelope</param>
        /// <returns>Handle result</returns>
        public abstract Task HandleMessage(TConnection connection, IncomingMessage message);

        public override Task HandleMessage(BaseWebSocketConnection connection, IncomingMessage message)
        {
            HandleMessage((TConnection)connection, message);
            return Task.CompletedTask;
        }
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
