using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class RequestContext : ProcessorContext
    {
        public RequestContext(EffectProcessorsContainer effectProcessors) 
            : base(effectProcessors)
        {
            Request = (RequestQuantum)Envelope.Message;
        }

        public RequestQuantum Request { get; }
    }
}
