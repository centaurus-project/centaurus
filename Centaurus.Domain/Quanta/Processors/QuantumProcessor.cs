using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public abstract class QuantumProcessor<T> : IQuantumProcessor<T>, IQuantumProcessor
        where T : ProcessorContext
    {
        public abstract MessageTypes SupportedMessageType { get; }

        public abstract Task<QuantumResultMessage> Process(T context);

        public abstract Task Validate(T context);

        Task IQuantumProcessor.Validate(object context)
        {
            return Validate((T)context);
        }

        Task<QuantumResultMessage> IQuantumProcessor.Process(object context)
        {
            return Process((T)context);
        }

        public abstract T GetContext(EffectProcessorsContainer container);

        ProcessorContext IQuantumProcessor.GetContext(EffectProcessorsContainer container) => GetContext(container);
    }

    public abstract class QuantumProcessor : QuantumProcessor<ProcessorContext>
    {
        public override ProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new ProcessorContext(container);
        }
    }

    public abstract class RequestQuantumProcessor : QuantumProcessor<RequestContext>
    {
        public override RequestContext GetContext(EffectProcessorsContainer container)
        {
            return new RequestContext(container);
        }
    }
}
