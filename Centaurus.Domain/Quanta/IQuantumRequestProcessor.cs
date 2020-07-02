using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public interface IQuantumRequestProcessor
    {
        public MessageTypes SupportedMessageType { get; }

        /// <summary>
        /// Validate quantum request preconditions.
        /// </summary>
        /// <param name="envelope">Quantum request</param>
        public Task Validate(MessageEnvelope envelope);

        /// <summary>
        /// Execute quantum request and generate response message.
        /// </summary>
        /// <param name="envelope">Quantum request</param>
        /// <param name="effectProcessorsContainer">Current context effects processor container</param>
        public Task<ResultMessage> Process(MessageEnvelope envelope, EffectProcessorsContainer effectProcessorsContainer);
    }
}
