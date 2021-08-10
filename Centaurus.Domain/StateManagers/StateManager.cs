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

        public StateManager(ExecutionContext context, State initState)
            : base(context)
        {
            SetState(initState);
        }

        public State State { get; private set; }

        public void Failed(Exception exc)
        {
            SetState(State.Failed, exc);
        }

        private void SetState(State state, Exception exc = null)
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

        private Dictionary<RawPubKey, ConnectedAuditor> connectedAuditors = new Dictionary<RawPubKey, ConnectedAuditor>();

        private bool HasMajority => Context.HasMajority(connectedAuditors.Count(a => a.Value.IsReady), false);

        public void Stopped()
        {
            SetState(State.Stopped);
        }

        public int ConnectedAuditorsCount => connectedAuditors.Count;

        public List<RawPubKey> ConnectedAuditors => connectedAuditors.Keys.ToList();

        public bool IsAuditorReady(RawPubKey pubKey)
        {
            if (pubKey == null)
                throw new ArgumentNullException(nameof(pubKey));

            lock (this)
            {
                if (!connectedAuditors.TryGetValue(pubKey, out var connectedAuditor))
                    return false;
                return connectedAuditor.IsReady;
            }
        }

        public void SetConnection(IAuditorConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            lock (this)
            {
                if (!connectedAuditors.TryGetValue(connection.PubKey, out var connectedAuditor))
                {
                    connectedAuditor = new ConnectedAuditor(Context, connection.PubKey);
                    connectedAuditors.Add(connection.PubKey, connectedAuditor);
                }
                connectedAuditor.SetConnection(connection);
                RefreshState();
            }
        }

        public void RemoveConnection(IAuditorConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            lock (this)
            {

                if (!connectedAuditors.TryGetValue(connection.PubKey, out var auditor))
                    return;

                auditor.RemoveConnection(connection);
                if (!auditor.HasAnyConnection)
                    connectedAuditors.Remove(connection.PubKey);
                RefreshState();
            }
        }

        public void Rised()
        {
            lock (this)
            {
                if (HasMajority)
                    SetState(State.Ready);
                else
                    SetState(State.Running);
            }
        }

        public void SetAuditorState(RawPubKey auditorPubKey, State state)
        {
            lock (this)
            {
                if (!connectedAuditors.TryGetValue(auditorPubKey, out var auditor) || auditor.AuditorState == state)
                    return;
                auditor.AuditorState = state;
                RefreshState();
            }
        }

        public void RefreshState()
        {
            lock (this)
            {
                if (State == State.Running && HasMajority)
                    SetState(State.Ready);
                else if (State == State.Ready && !HasMajority)
                    SetState(State.Running);
            }
        }

        public event Action<StateChangedEventArgs> StateChanged;
    }

    class ConnectedAuditor : ContextualBase
    {
        public ConnectedAuditor(ExecutionContext context, RawPubKey pubKey)
            : base(context)
        {
            PubKey = pubKey ?? throw new ArgumentNullException(nameof(pubKey));
        }

        public RawPubKey PubKey { get; }

        private IncomingAuditorConnection incoming;

        private OutgoingConnection outgoing;

        public void SetConnection(IAuditorConnection newConnection)
        {
            if (newConnection == null)
                throw new ArgumentNullException(nameof(newConnection));

            if (newConnection is IncomingAuditorConnection inConnection)
                SetConnection(inConnection);
            else if (newConnection is OutgoingConnection outConnection)
                SetConnection(outConnection);
            else
                throw new InvalidOperationException($"Unsupported type {newConnection.GetType().Name}.");
        }

        private void SetConnection(IncomingAuditorConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (incoming == connection)
                return;

            if (incoming != null)
                throw new InvalidOperationException($"Incoming connection for {PubKey.GetAccountId()} is already registered.");

            if (!connection.PubKey.Equals(PubKey))
                throw new InvalidOperationException($"Incoming connection has invalid public key {connection.PubKey.GetAccountId()}.");

            incoming = connection;
        }

        private void SetConnection(OutgoingConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (outgoing == connection)
                return;

            if (outgoing != null)
                throw new InvalidOperationException($"Incoming connection for {PubKey.GetAccountId()} is already registered.");

            if (!connection.PubKey.Equals(PubKey))
                throw new InvalidOperationException($"Incoming connection has invalid public key {connection.PubKey.GetAccountId()}.");

            outgoing = connection;
        }

        public void RemoveConnection(IAuditorConnection connection)
        {
            if (connection == incoming)
                incoming = null;
            else if (connection == outgoing)
                outgoing = null;
            else
                throw new InvalidOperationException($"Unsupported type {connection.GetType().Name}.");
        }

        public bool HasAnyConnection => incoming != null || outgoing != null;

        public State AuditorState { get; set; }

        public bool IsReady => AuditorState == State.Ready || AuditorState == State.Running && isConnectionReady();

        private static ConnectionState[] validConnectionStates = new[] { ConnectionState.Ready, ConnectionState.Validated };
        private bool isConnectionReady()
        {
            var connection = Context.IsAlpha ? (ConnectionBase)incoming : outgoing;
            return validConnectionStates.Contains(connection.ConnectionState);
        }
    }
}