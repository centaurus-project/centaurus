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
    /// <summary>
    /// Contains all registered handlers
    /// </summary>
    public class MessageHandlers<TConnection>: ContextualBase
        where TConnection : BaseWebSocketConnection
    {
        readonly ImmutableDictionary<MessageTypes, BaseMessageHandler<TConnection>> handlers;

        public MessageHandlers(ExecutionContext context)
            :base(context)
        {
            //TODO: rename auditor and alpha interfaces to more generic and clear
            var currentServerHandlerType = typeof(IAuditorMessageHandler);
            if (typeof(TConnection) == typeof(IncomingWebSocketConnection))
                currentServerHandlerType = typeof(IAlphaMessageHandler);

            var discoveredRequestProcessors = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(x => typeof(BaseMessageHandler<TConnection>).IsAssignableFrom(x)
                    && currentServerHandlerType.IsAssignableFrom(x)
                    && !x.IsInterface
                    && !x.IsAbstract);

            var processors = new Dictionary<MessageTypes, BaseMessageHandler<TConnection>>();
            foreach (var processorType in discoveredRequestProcessors)
            {
                var instance = Activator.CreateInstance(processorType, new object[] { Context }) as BaseMessageHandler<TConnection>;
                if (processors.ContainsKey(instance.SupportedMessageType))
                    throw new Exception($"Handler for message type {instance.SupportedMessageType} is already registered");
                processors.Add(instance.SupportedMessageType, instance);
            }
            handlers = processors.ToImmutableDictionary();
        }

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

        public async Task<bool> HandleMessage(BaseWebSocketConnection connetction, IncomingMessage message)
        {
            return await HandleMessage((TConnection)connetction, message);
        }
    }
}