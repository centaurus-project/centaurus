using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{

    public abstract class MessageHandlers : ContextualBase
    {
        public MessageHandlers(ExecutionContext context)
            :base(context)
        {

        }

        public abstract Task<bool> HandleMessage(BaseWebSocketConnection connetction, IncomingMessage message);
    }

    /// <summary>
    /// Contains all registered handlers
    /// </summary>
    public class MessageHandlers<TConnection, TContext>: MessageHandlers, IContextual<TContext>
        where TConnection : BaseWebSocketConnection
        where TContext : ExecutionContext
    {
        readonly ImmutableDictionary<MessageTypes, BaseMessageHandler<TConnection, TContext>> handlers;

        public MessageHandlers(TContext context)
            :base(context)
        {
            var currentServerHandlerType = typeof(IAuditorMessageHandler);
            if (context.IsAlpha)
                currentServerHandlerType = typeof(IAlphaMessageHandler);

            var discoveredRequestProcessors = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(x => typeof(BaseMessageHandler<TConnection, TContext>).IsAssignableFrom(x)
                    && currentServerHandlerType.IsAssignableFrom(x)
                    && !x.IsInterface
                    && !x.IsAbstract);

            var processors = new Dictionary<MessageTypes, BaseMessageHandler<TConnection, TContext>>();
            foreach (var processorType in discoveredRequestProcessors)
            {
                var instance = Activator.CreateInstance(processorType, new object[] { Context }) as BaseMessageHandler<TConnection, TContext>;
                if (processors.ContainsKey(instance.SupportedMessageType))
                    throw new Exception($"Handler for message type {instance.SupportedMessageType} is already registered");
                processors.Add(instance.SupportedMessageType, instance);
            }
            handlers = processors.ToImmutableDictionary();
        }

        public new TContext Context => (TContext)base.Context;

        public async Task<bool> HandleMessage(TConnection connetction, IncomingMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (!handlers.TryGetValue(message.Envelope.Message.MessageType, out var handler))
                return false;

            Context.ExtensionsManager.BeforeValidateMessage(connetction, message.Envelope);
            await handler.Validate(connetction, message);
            Context.ExtensionsManager.AfterValidateMessage(connetction, message.Envelope);
            Context.ExtensionsManager.BeforeHandleMessage(connetction, message.Envelope);
            await handler.HandleMessage(connetction, message);
            Context.ExtensionsManager.AfterHandleMessage(connetction, message.Envelope);
            return true;
        }

        public override async Task<bool> HandleMessage(BaseWebSocketConnection connetction, IncomingMessage message)
        {
            return await HandleMessage((TConnection)connetction, message);
        }
    }
}
