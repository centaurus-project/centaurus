using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public interface IQuantumRequestProcessor
    {
        public MessageTypes SupportedMessageType { get; }

        /// <summary>
        /// Validate quantum request preconditions.
        /// </summary>
        /// <param name="envelope">Quantum request</param>
        public void Validate(MessageEnvelope envelope);

        /// <summary>
        /// Execute quantum request and generate response message.
        /// </summary>
        /// <param name="envelope">Quantum request</param>
        public ResultMessage Process(MessageEnvelope envelope);
    }
}
