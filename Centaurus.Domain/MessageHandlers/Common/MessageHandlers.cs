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
    public static class MessageHandlers<T>
        where T : BaseWebSocketConnection
    {
        static ImmutableDictionary<MessageTypes, BaseMessageHandler<T>> handlers;
        static CentaurusContext context;

        public static void Init(CentaurusContext context)
        {
            MessageHandlers<T>.context = context ?? throw new ArgumentNullException(nameof(context));
            var currentServerHandlerType = typeof(IAuditorMessageHandler);
            if (context.IsAlpha)
                currentServerHandlerType = typeof(IAlphaMessageHandler);

            var discoveredRequestProcessors = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(x => typeof(BaseMessageHandler<T>).IsAssignableFrom(x)
                    && currentServerHandlerType.IsAssignableFrom(x)
                    && !x.IsInterface
                    && !x.IsAbstract);

            var processors = new Dictionary<MessageTypes, BaseMessageHandler<T>>();
            foreach (var processorType in discoveredRequestProcessors)
            {
                var instance = Activator.CreateInstance(processorType) as BaseMessageHandler<T>;
                if (processors.ContainsKey(instance.SupportedMessageType))
                    throw new Exception($"Handler for message type {instance.SupportedMessageType} is already registered");
                processors.Add(instance.SupportedMessageType, instance);
            }
            handlers = processors.ToImmutableDictionary();
        }

        public static async Task<bool> HandleMessage(T connetction, IncomingMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (!handlers.TryGetValue(message.Envelope.Message.MessageType, out var handler))
                return false;

            context.ExtensionsManager.BeforeValidateMessage(connetction, message.Envelope);
            await handler.Validate(connetction, message);
            context.ExtensionsManager.AfterValidateMessage(connetction, message.Envelope);
            context.ExtensionsManager.BeforeHandleMessage(connetction, message.Envelope);
            await handler.HandleMessage(connetction, message);
            context.ExtensionsManager.AfterHandleMessage(connetction, message.Envelope);
            return true;
        }
    }
}
