using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Centaurus.Domain
{
    public class StateChangedEventArgs : EventArgs
    {
        public StateChangedEventArgs(State state, State prevState)
        {
            State = state;
            PrevState = prevState;
        }

        public State State { get; }

        public State PrevState { get; }
    }

    public class StateManager : ContextualBase
    {
        private Logger logger = LogManager.GetCurrentClassLogger();

        public StateManager(ExecutionContext context)
            : base(context)
        {
        }

        public virtual State State { get; private set; }

        public void SetState(State state, Exception exc = null)
        {
            lock (this)
            {
                if (exc != null)
                    logger.Error(exc);
                if (State != state)
                {
                    var stateArgs = new StateChangedEventArgs(state, State);
                    State = state;

                    StateChanged?.Invoke(stateArgs);
                }
            }
        }

        private Dictionary<RawPubKey, AuditorConnection> ConnectedAuditors = new Dictionary<RawPubKey, AuditorConnection>();

        public bool HasMajority
        {
            get
            {
                return Context.HasMajority(ConnectedAuditors.Count(a => a.Value.State == ConnectionState.Ready), false);
            }
        }

        public int ConnectedAuditorsCount => ConnectedAuditors.Count;

        public void RegisterConnection(IAuditorConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            lock (this)
            {
                if (!ConnectedAuditors.TryGetValue(connection.PubKey, out var auditorConnection))
                    auditorConnection = new AuditorConnection(Context, connection.PubKey);
                auditorConnection.RegisterConnection(connection);
                if (Context.IsAlpha && HasMajority && State == State.Running)
                    State = State.Ready;
            }
        }

        public void RemoveConnection(IAuditorConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            lock (this)
            {

                if (!ConnectedAuditors.TryGetValue(connection.PubKey, out var auditorConnection))
                    return;

                auditorConnection.RemoveConnection(connection);
                if (!auditorConnection.HasAnyConnection)
                    ConnectedAuditors.Remove(connection.PubKey);

                if (Context.IsAlpha && !HasMajority && State == State.Ready)
                    State = State.Running;
            }
        }

        public void Rised()
        {
            lock (this)
            {
                if (HasMajority)
                    State = State.Ready;
                else
                    State = State.Running;
            }
        }

        public event Action<StateChangedEventArgs> StateChanged;
    }

    class AuditorConnection : ContextualBase
    {
        public AuditorConnection(ExecutionContext context, RawPubKey pubKey)
            : base(context)
        {
            PubKey = pubKey ?? throw new ArgumentNullException(nameof(pubKey));
        }

        public RawPubKey PubKey { get; }

        private IncomingAuditorConnection incoming;

        private OutgoingConnection outgoing;

        public void RegisterConnection(IAuditorConnection newConnection)
        {
            if (newConnection == null)
                throw new ArgumentNullException(nameof(newConnection));

            if (newConnection is IncomingAuditorConnection inConnection)
                RegisterConnection(inConnection);
            else if (newConnection is OutgoingConnection outConnection)
                RegisterConnection(outConnection);
            else
                throw new InvalidOperationException($"Unsupported type {newConnection.GetType().Name}.");
        }

        private void RegisterConnection(IncomingAuditorConnection newConnection)
        {
            if (incoming != null)
                throw new InvalidOperationException($"Incoming connection for {PubKey.GetAccountId()} is already registered.");

            if (!newConnection.PubKey.Equals(PubKey))
                throw new InvalidOperationException($"Incoming connection has invalid public key {newConnection.PubKey.GetAccountId()}.");

            incoming = newConnection;
        }

        private void RegisterConnection(OutgoingConnection newConnection)
        {
            if (newConnection == null)
                throw new ArgumentNullException(nameof(newConnection));

            if (outgoing != null)
                throw new InvalidOperationException($"Incoming connection for {PubKey.GetAccountId()} is already registered.");

            if (!newConnection.PubKey.Equals(PubKey))
                throw new InvalidOperationException($"Incoming connection has invalid public key {newConnection.PubKey.GetAccountId()}.");

            outgoing = newConnection;
        }

        public void RemoveConnection(IAuditorConnection connection)
        {
            if (connection is IncomingAuditorConnection)
                incoming = null;
            else if (connection is OutgoingConnection)
                outgoing = null;
            else
                throw new InvalidOperationException($"Unsupported type {connection.GetType().Name}.");
        }

        public bool HasAnyConnection => incoming != null || outgoing != null;

        public ConnectionState State
        {
            get
            {
                var state = default(ConnectionState?);
                if (Context.IsAlpha)
                    state = incoming?.ConnectionState;
                else
                    state = outgoing?.ConnectionState;
                return state ?? ConnectionState.Closed;
            }
        }
    }
}