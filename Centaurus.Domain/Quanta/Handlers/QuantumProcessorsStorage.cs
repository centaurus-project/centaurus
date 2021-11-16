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
    public class QuantumProcessorsStorage: ContextualBase
    {
        public QuantumProcessorsStorage(ExecutionContext context)
            :base(context)
        {
            var discoveredRequestProcessors = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(x => typeof(QuantumProcessorBase).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract);

            var _processors = new Dictionary<string, QuantumProcessorBase>();
            foreach (var processorType in discoveredRequestProcessors)
            {
                var instance = (QuantumProcessorBase)Activator.CreateInstance(processorType, new [] { Context });
                if (_processors.ContainsKey(instance.SupportedMessageType))
                    throw new Exception($"Processor for message type {instance.SupportedMessageType} is already registered");

                _processors.Add(instance.SupportedMessageType, instance);
            }
            processors = _processors.ToImmutableDictionary();
        }

        readonly ImmutableDictionary<string, QuantumProcessorBase> processors;

        public bool TryGetValue(string messageType, out QuantumProcessorBase processor)
        {
            return processors.TryGetValue(messageType, out processor);
        }
    }
}
