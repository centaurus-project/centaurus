using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    /// <summary>
    /// In some cases we have a lot of duplicate actions on validation and processing envelopes. 
    /// For optimization, we need a context to store already resulted data.
    /// </summary>
    public class ProcessorContext
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="effectProcessors">Current context effects processor container</param>
        public ProcessorContext(EffectProcessorsContainer effectProcessors)
        {
            EffectProcessors = effectProcessors ?? throw new ArgumentNullException(nameof(effectProcessors));
        }

        public MessageEnvelope Envelope => EffectProcessors.Envelope;

        public EffectProcessorsContainer EffectProcessors { get; }

        public ExecutionContext CentaurusContext => EffectProcessors.Context;
    }
}
