using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test.Client
{
    /// <summary>
    /// Contains all registered handlers
    /// </summary>
    public static class MessageHandlers
    {
        static ImmutableDictionary<MessageTypes, BaseClientMessageHandler> Handlers;

        public static void Init()
        {
            var discoveredRequestProcessors = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(x => typeof(BaseClientMessageHandler).IsAssignableFrom(x)
                    && !x.IsInterface
                    && !x.IsAbstract);

            var processors = new Dictionary<MessageTypes, BaseClientMessageHandler>();
            foreach (var processorType in discoveredRequestProcessors)
            {
                var instance = Activator.CreateInstance(processorType) as BaseClientMessageHandler;
                if (processors.ContainsKey(instance.SupportedMessageType))
                    throw new Exception($"Handler for message type {instance.SupportedMessageType} is already registered");
                processors.Add(instance.SupportedMessageType, instance);
            }
            Handlers = processors.ToImmutableDictionary();
        }

        public static async Task<bool> HandleMessage(UserWebSocketConnection connetction, MessageEnvelope envelope)
        {
            if (!Handlers.ContainsKey(envelope.Message.MessageType))
                return false;

            var handler = Handlers[envelope.Message.MessageType];
            await handler.Validate(connetction, envelope);
            await handler.HandleMessage(connetction, envelope);
            return true;
        }
    }
}
