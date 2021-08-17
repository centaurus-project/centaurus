using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class RequestContext : ProcessorContext
    {
        public RequestContext(ExecutionContext context, Quantum quantum, AccountWrapper account)
            : base(context, quantum, account)
        {
            if (account == null)
                throw new ArgumentNullException("Source account cannot be null for requests.");
        }

        public RequestQuantumBase Request => (RequestQuantumBase)Quantum;
    }
}
