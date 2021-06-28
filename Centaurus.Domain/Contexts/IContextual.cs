using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public interface IContextual
    {
        public ExecutionContext Context { get; }
    }

    public abstract class ContextualBase : IContextual
    {
        public ContextualBase(ExecutionContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ExecutionContext Context { get; }
    }
}