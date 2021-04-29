using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public interface IQuantumRequestProcessor<T>
        where T: ProcessorContext
    {
        public MessageTypes SupportedMessageType { get; }

        /// <summary>
        /// Validate quantum request preconditions.
        /// </summary>
        /// <param name="context">Request context</param>
        public Task Validate(T context);

        /// <summary>
        /// Execute quantum request and generate response message.
        /// </summary>
        /// <param name="context">Request context</param>
        public Task<ResultMessage> Process(T context);

        /// <summary>
        /// Generates context for the processor.
        /// </summary>
        /// <param name="context">Request context</param>
        public T GetContext(EffectProcessorsContainer container);
    }

    public interface IQuantumRequestProcessor
    {
        public MessageTypes SupportedMessageType { get; }

        /// <summary>
        /// Validate quantum request preconditions.
        /// </summary>
        /// <param name="context">Request context</param>
        public Task Validate(object context);

        /// <summary>
        /// Execute quantum request and generate response message.
        /// </summary>
        /// <param name="context">Request context</param>
        public Task<ResultMessage> Process(object context);

        /// <summary>
        /// Generates context for the processor.
        /// </summary>
        /// <param name="context">Request context</param>
        public ProcessorContext GetContext(EffectProcessorsContainer container);
    }
}
