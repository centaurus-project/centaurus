using Centaurus.Domain.Models;
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
            SourceAccount = effectProcessors.AccountWrapper ?? throw new ArgumentNullException(nameof(effectProcessors.AccountWrapper));
        }

        public RequestQuantum Request { get; }

        public AccountWrapper SourceAccount { get; }
    }
}
