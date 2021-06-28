using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus.Domain
{
    public class QuantumProcessorsStorage
    {
        public QuantumProcessorsStorage()
        {
            var discoveredRequestProcessors = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(x => typeof(IQuantumProcessor).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract);

            var _processors = new Dictionary<MessageTypes, IQuantumProcessor>();
            foreach (var processorType in discoveredRequestProcessors)
            {
                var instance = (IQuantumProcessor)Activator.CreateInstance(processorType);
                if (_processors.ContainsKey(instance.SupportedMessageType))
                    throw new Exception($"Processor for message type {instance.SupportedMessageType} is already registered");

                _processors.Add(instance.SupportedMessageType, instance);
            }
            processors = _processors.ToImmutableDictionary();
        }

        readonly ImmutableDictionary<MessageTypes, IQuantumProcessor> processors;

        public bool TryGetValue(MessageTypes messageType, out IQuantumProcessor processor)
        {
            return processors.TryGetValue(messageType, out processor);
        }
    }
}
