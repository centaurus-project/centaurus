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

        public event Action<StateChangedEventArgs> StateChanged;


        public void Init(State state)
        {
            lock (syncRoot)
            {
                if (State != State.Undefined)
                    throw new InvalidOperationException("Context is already initialized.");

                if (state == State.Undefined)
                    throw new InvalidOperationException($"Init state cannot be {state}.");

                UpdateState(state);
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
                UpdateState(State.Running);
            //after node successfully started, the pending quanta can be removed
            Context.PersistentStorage.DeletePendingQuanta();
        }

        public void SetAuditorState(RawPubKey auditorPubKey, State state)
        {
            lock (statesSyncRoot)
            {
                logger.Trace($"Auditor's {auditorPubKey.GetAccountId()} state {state} received.");
                if (!connectedAuditors.TryGetValue(auditorPubKey, out var currentState) && state == currentState)
                    return; //state didn't change
                connectedAuditors[auditorPubKey] = state;
                logger.Trace($"Auditor's {auditorPubKey.GetAccountId()} state {state} set.");
                //update current node state
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
                    //update current node state
                    UpdateState();
                }
            }
        }

        /// <summary>
        /// Updates last processed by alpha apex
        /// </summary>
        /// <param name="constellationApex"></param>
        public void UpdateAlphaApex(ulong constellationApex)
        {
            lock (syncRoot)
            {
                AlphaApex = constellationApex;
            }
        }

        /// <summary>
        /// Updates current node delay
        /// </summary>
        public void UpdateDelay()
        {
            if (Context.IsAlpha)
                return;
            lock (syncRoot)
            {
                var isDelayed = State.Chasing == State;
                if (isDelayed)
                {
                    if (AlphaApex <= Context.QuantumStorage.CurrentApex || AlphaApex - Context.QuantumStorage.CurrentApex < RunningDelayTreshold)
                        UpdateState(State.Running);
                }
                else
                {
                    if (AlphaApex > Context.QuantumStorage.CurrentApex && AlphaApex - Context.QuantumStorage.CurrentApex > ChasingDelayTreshold)
                        UpdateState(State.Chasing);
                }
            }
        }

        private const ulong ChasingDelayTreshold = 10_000;
        private const ulong RunningDelayTreshold = 1_000;
        private ulong AlphaApex;
        private object syncRoot = new { };
        private object statesSyncRoot = new { };
        private Dictionary<RawPubKey, State> connectedAuditors = new Dictionary<RawPubKey, State>();

        private void UpdateState(State? state = null)
        {
            if (!state.HasValue) //state can be null if trying update on auditor's state change
                state = State;
            if (state == State.Running && IsConstellationReady())
                SetState(State.Ready);
            else if (state == State.Ready && !IsConstellationReady())
                SetState(State.Running);
            else
                SetState(state.Value);
        }

        private bool IsConstellationReady()
        {
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
                    //if auditor doesn't have connections with another auditors, we only need to verify alpha's state
                    return connectedAuditors.TryGetValue(Context.Constellation.Alpha, out var alphaState) && (alphaState == State.Ready || alphaState == State.Running);
            }
        }

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
                var updateMessage = new StateUpdateMessage { State = State }
                    .CreateEnvelope<MessageEnvelopeSignless>()
                    .Sign(Context.Settings.KeyPair);
                Context.NotifyAuditors(updateMessage);
                logger.Trace($"State update {state} sent.");
            }
        }

    }
}