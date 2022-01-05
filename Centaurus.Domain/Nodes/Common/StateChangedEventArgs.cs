using Centaurus.Models;
using System;

namespace Centaurus.Domain
{
    public class StateChangedEventArgs : EventArgs
    {
        public StateChangedEventArgs(NodeBase source, State state, State prevState)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            State = state;
            PrevState = prevState;
        }

        public NodeBase Source { get; }

        public State State { get; }

        public State PrevState { get; }
    }
}