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

        public static void Init()
        {
            var currentServerHandlerType = typeof(IAuditorMessageHandler);
            if (Global.IsAlpha)
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

        public static async Task<bool> HandleMessage(T connetction, MessageEnvelope envelope)
        {
            if (!handlers.ContainsKey(envelope.Message.MessageType))
                return false;

            var handler = handlers[envelope.Message.MessageType];
            Global.ExtensionsManager.BeforeValidateMessage(connetction, envelope);
            await handler.Validate(connetction, envelope);
            Global.ExtensionsManager.AfterValidateMessage(connetction, envelope);
            Global.ExtensionsManager.BeforeHandleMessage(connetction, envelope);
            await handler.HandleMessage(connetction, envelope);
            Global.ExtensionsManager.AfterHandleMessage(connetction, envelope);
            return true;
        }
    }
}
