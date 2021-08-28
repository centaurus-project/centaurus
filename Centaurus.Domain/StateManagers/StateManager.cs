using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public State State { get; private set; }

        public void Init(State state)
        {
            lock (syncRoot)
            {
                if (State != State.Undefined)
                    throw new InvalidOperationException("Context is already initialized.");

                SetState(state);
            }
        }

        public void Stopped()
        {
            lock (syncRoot)
                SetState(State.Stopped);
        }

        public void Failed(Exception exc)
        {
            lock (syncRoot)
                SetState(State.Failed, exc);
        }

        private object syncRoot = new { };
        private void SetState(State state, Exception exc = null)
        {
            if (exc != null)
                logger.Error(exc);
            if (State != state)
            {
                logger.Trace($"State update: new state: {state}, prev state: {State}");
                var stateArgs = new StateChangedEventArgs(state, State);
                State = state;

                StateChanged?.Invoke(stateArgs);

                Context.NotifyAuditors(new StateUpdateMessage { State = State }.CreateEnvelope<MessageEnvelopeSigneless>());
                logger.Trace($"State update {state} sent.");
            }
        }

        private object statesSyncRoot = new { };
        private Dictionary<RawPubKey, State> connectedAuditors = new Dictionary<RawPubKey, State>();

        private bool IsConstellationReady()
        {
            //get
            //{
                lock (statesSyncRoot)
                {
                    //if Prime node than it must be connected with other nodes
                    if (Context.RoleManager.ParticipationLevel == CentaurusNodeParticipationLevel.Prime)
                    {
                        //if current server is 
                        var isAlphaReady = Context.IsAlpha && (State == State.Ready || State == State.Running);
                        var connectedCount = 0;
                        foreach (var auditorState in connectedAuditors)
                        {
                            if (auditorState.Value != State.Ready && auditorState.Value != State.Running)
                                continue;
                            if (Context.Constellation.Alpha == auditorState.Key)
                                isAlphaReady = true;
                            connectedCount++;
                        }
                        return isAlphaReady && Context.HasMajority(connectedCount, false);
                    }
                    else
                        return connectedAuditors.TryGetValue(Context.Constellation.Alpha, out var alphaState) && (alphaState == State.Ready || alphaState == State.Running);
                }
            //}
        }

        public int ConnectedAuditorsCount
        {
            get
            {
                lock (statesSyncRoot)
                    return connectedAuditors.Count;
            }
        }

        public List<RawPubKey> ConnectedAuditors
        {
            get
            {
                lock (statesSyncRoot)
                    return connectedAuditors.Keys.ToList();
            }
        }

        public bool IsAuditorReady(RawPubKey pubKey)
        {
            if (pubKey == null)
                throw new ArgumentNullException(nameof(pubKey));

            lock (statesSyncRoot)
            {
                if (!connectedAuditors.TryGetValue(pubKey, out var state))
                    return false;
                return state == State.Ready;
            }
        }

        public bool IsAuditorRunning(RawPubKey pubKey)
        {
            if (pubKey == null)
                throw new ArgumentNullException(nameof(pubKey));

            lock (statesSyncRoot)
            {
                if (!connectedAuditors.TryGetValue(pubKey, out var state))
                    return false;
                return state == State.Running || state == State.Ready || state == State.Chasing;
            }
        }

        public void Rised()
        {
            lock (syncRoot)
            {
                if (IsConstellationReady())
                    SetState(State.Ready);
                else
                    SetState(State.Running);
            }
        }

        public void SetAuditorState(RawPubKey auditorPubKey, State state)
        {
            lock (statesSyncRoot)
            {
                logger.Trace($"Auditor's {auditorPubKey.GetAccountId()} state {state} received.");
                if (!connectedAuditors.TryGetValue(auditorPubKey, out var currentState) && state == currentState)
                    return;
                connectedAuditors[auditorPubKey] = state;
                logger.Trace($"Auditor's {auditorPubKey.GetAccountId()} state {state} set.");
                UpdateState();
            }
        }

        public void RemoveAuditorState(RawPubKey auditorPubKey)
        {
            lock (statesSyncRoot)
            {
                if (connectedAuditors.Remove(auditorPubKey, out _))
                {
                    //remove state from catchup if presented
                    Context.Catchup.RemoveState(auditorPubKey);
                    UpdateState();
                }
            }
        }

        const ulong ChasingDelayTreshold = 10_000;
        const ulong RunningDelayTreshold = 1_000;

        public void UpdateDelay(ulong apexDiff)
        {
            lock (syncRoot)
            {
                if (apexDiff > ChasingDelayTreshold && State == State.Ready)
                    SetState(State.Chasing); //node is too behind the Alpha
                else if (apexDiff < RunningDelayTreshold && State == State.Chasing)
                    SetState(State.Running); //node riched Alpha
            }
        }

        private void UpdateState()
        {
            lock (syncRoot)
            {
                if (State == State.Running && IsConstellationReady())
                    SetState(State.Ready);
                else if (State == State.Ready && !IsConstellationReady())
                    SetState(State.Running);
            }
        }

        public event Action<StateChangedEventArgs> StateChanged;
    }
}