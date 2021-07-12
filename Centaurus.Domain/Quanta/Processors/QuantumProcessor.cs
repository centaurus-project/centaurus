using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Domain.Models;
using Centaurus.Models;

namespace Centaurus.Domain
{
    public abstract class QuantumProcessorBase : ContextualBase
    {
        public QuantumProcessorBase(ExecutionContext context)
            : base(context)
        {

        }

        public abstract MessageTypes SupportedMessageType { get; }


        /// <summary>
        /// Execute quantum request and generate response message.
        /// </summary>
        /// <param name="context">Request context</param>
        public abstract Task<QuantumResultMessage> Process(object context);

        /// <summary>
        /// Validate quantum request preconditions.
        /// </summary>
        /// <param name="context">Request context</param>
        public abstract Task Validate(object context);

        /// <summary>
        /// Generates context for the processor.
        /// </summary>
        public abstract ProcessorContext GetContext(MessageEnvelope messageEnvelope, AccountWrapper account);
    }

    public abstract class QuantumProcessorBase<T> : QuantumProcessorBase
        where T : ProcessorContext
    {
        public QuantumProcessorBase(ExecutionContext context)
            :base(context)
        {

        }

        public override Task<QuantumResultMessage> Process(object context)
        {
            return Process((T)context);
        }

        public abstract Task<QuantumResultMessage> Process(T context);


        public override Task Validate(object context)
        {
            return Validate((T)context);
        }

        public abstract Task Validate(T context);
    }

    public abstract class QuantumProcessor : QuantumProcessorBase<ProcessorContext>
    {
        public QuantumProcessor(ExecutionContext context)
            :base(context)
        {

        }

        public override ProcessorContext GetContext(MessageEnvelope messageEnvelope, AccountWrapper account)
        {
            return new ProcessorContext(Context, messageEnvelope, account);
        }
    }

    public abstract class RequestQuantumProcessor : QuantumProcessorBase<RequestContext>
    {
        public RequestQuantumProcessor(ExecutionContext context)
            : base(context)
        {

        }

        public override ProcessorContext GetContext(MessageEnvelope messageEnvelope, AccountWrapper account)
        {
            return new RequestContext(Context, messageEnvelope, account);
        }
    }
}
