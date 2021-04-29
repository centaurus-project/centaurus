using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class StateChangedEventArgs : EventArgs
    {
        public StateChangedEventArgs(ApplicationState state, ApplicationState prevState)
        {
            State = state;
            PrevState = prevState;
        }

        public ApplicationState State { get; }

        public ApplicationState PrevState { get; }
    }

    public abstract class StateManager : ContextualBase
    {
        public StateManager(ExecutionContext context)
            : base(context)
        {
        }

        private ApplicationState state;
        public virtual ApplicationState State
        {
            get
            {
                return state;
            }
            set
            {
                if (state != value)
                    lock (this)
                    {
                        if (state != value)
                        {
                            var stateArgs = new StateChangedEventArgs(value, state);
                            state = value;
                            StateChanged?.Invoke(stateArgs);
                        }
                    }
            }
        }

        public event Action<StateChangedEventArgs> StateChanged;
    }

    public abstract class StateManager<TContext> : StateManager, IContextual<TContext>
        where TContext: ExecutionContext
    {
        public StateManager(TContext context)
            : base(context)
        {
        }

        public new TContext Context => (TContext)base.Context;
    }
}
