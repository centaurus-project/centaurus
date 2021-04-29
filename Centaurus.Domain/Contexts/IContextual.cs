using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public interface IContextual
    {
        public ExecutionContext Context { get; }
    }

    public interface IContextual<TContext>
        where TContext: ExecutionContext
    { 
        public TContext Context { get; }
    }

    public abstract class ContextualBase : IContextual
    {
        public ContextualBase(ExecutionContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ExecutionContext Context { get; }
    }

    public abstract class ContextualBase<TContext> : ContextualBase, IContextual<TContext>
        where TContext : ExecutionContext
    {
        public ContextualBase(TContext context)
            :base(context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public new TContext Context { get; }
    }
}