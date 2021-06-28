using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
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

    public class StateManager : ContextualBase
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



        private Dictionary<RawPubKey, ConnectionState> ConnectedAuditors = new Dictionary<RawPubKey, ConnectionState>();

        public bool HasMajority => Context.HasMajority(ConnectedAuditors.Count(a => a.Value == ConnectionState.Ready), false);

        public int ConnectedAuditorsCount => ConnectedAuditors.Count;

        public void RegisterAuditorState(RawPubKey rawPubKey, ConnectionState connectionState)
        {
            lock (this)
            {
                ConnectedAuditors[rawPubKey] = connectionState;
                if (Context.IsAlpha && HasMajority && State == ApplicationState.Running)
                    State = ApplicationState.Ready;
            }
        }

        public void AuditorConnectionClosed(RawPubKey rawPubKey)
        {
            lock (this)
            {
                ConnectedAuditors.Remove(rawPubKey);
                if (Context.IsAlpha && !HasMajority && State == ApplicationState.Ready)
                    State = ApplicationState.Running;
            }
        }

        public void AlphaRised()
        {
            lock (this)
            {
                if (!Context.IsAlpha)
                    return;

                if (HasMajority)
                    State = ApplicationState.Ready;
                else
                    State = ApplicationState.Running;
            }
        }

        public event Action<StateChangedEventArgs> StateChanged;
    }
}
