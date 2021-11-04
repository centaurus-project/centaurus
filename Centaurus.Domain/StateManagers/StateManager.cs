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
                    return auditors.Count(a => a.Value.CurrentState.HasValue);
            }
        }

        public AuditorState GetAuditorState(RawPubKey pubKey)
        {
            lock (statesSyncRoot)
            {
                if (!auditors.TryGetValue(pubKey, out var auditorState))
                    throw new UnauthorizedException($"{pubKey.GetAccountId()} is not auditor.");
                return auditorState;
            }
        }

        public List<RawPubKey> ConnectedAuditors
        {
            get
            {
                lock (statesSyncRoot)
                    return auditors.Keys.ToList();
            }
        }

        public void Rised()
        {
            lock (syncRoot)
                UpdateState(State.Running);
            //after node successfully started, the pending quanta can be deleted
            Context.PersistentStorage.DeletePendingQuanta();
        }

        public void SetAuditors(List<RawPubKey> auditorPubkeys)
        {
            lock (statesSyncRoot)
            {
                var currentAuditors = auditors;
                auditors.Clear();
                var currentAuditorKey = (RawPubKey)Context.Settings.KeyPair;
                foreach (var auditor in auditorPubkeys)
                {
                    if (auditor.Equals(currentAuditorKey))
                        continue; //skip current
                    //if auditor's state already presented, add it. Otherwise create and add new state instance.
                    if (currentAuditors.TryGetValue(auditor, out var auditorState))
                        auditors.Add(auditor, auditorState);
                    else
                        auditors.Add(auditor, new AuditorState());
                }
                //update current node state
                UpdateState();
            }
        }

        public void SetAuditorState(RawPubKey auditorPubKey, State state)
        {
            lock (statesSyncRoot)
            {
                logger.Trace($"Auditor's {auditorPubKey.GetAccountId()} state {state} received.");
                if (!auditors.TryGetValue(auditorPubKey, out var currentState))
                    throw new Exception($"{auditorPubKey.GetAccountId()} is not auditor.");

                if (currentState.CurrentState == state)
                    return; //state didn't change

                currentState.CurrentState = state;
                logger.Trace($"Auditor's {auditorPubKey.GetAccountId()} state {state} set.");
                //update current node state
                UpdateState();
            }
        }

        public void RemoveAuditorState(RawPubKey auditorPubKey)
        {
            lock (statesSyncRoot)
            {
                if (auditors.TryGetValue(auditorPubKey, out var currentState))
                {
                    currentState.CurrentState = null;
                    //remove state from catchup if presented
                    Context.Catchup.RemoveState(auditorPubKey);
                    //update current node state
                    UpdateState();
                }
                else
                    throw new Exception($"{auditorPubKey.GetAccountId()} is not auditor.");
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
                var currentApex = Context.QuantumHandler.CurrentApex;
                if (isDelayed)
                {
                    if (AlphaApex <= currentApex || AlphaApex - currentApex < RunningDelayTreshold)
                        UpdateState(State.Running);
                    logger.Info($"Current node delay is {AlphaApex - currentApex}");
                }
                else
                {
                    if (AlphaApex > currentApex && AlphaApex - currentApex > ChasingDelayTreshold)
                    {
                        logger.Info($"Current node delay is {AlphaApex - currentApex}");
                        UpdateState(State.Chasing);
                    }
                }
            }
        }

        private const ulong ChasingDelayTreshold = 50_000;
        private const ulong RunningDelayTreshold = 10_000;
        private ulong AlphaApex;
        private object syncRoot = new { };
        private object statesSyncRoot = new { };
        private Dictionary<RawPubKey, AuditorState> auditors = new Dictionary<RawPubKey, AuditorState>();

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
                    foreach (var auditorState in auditors)
                    {
                        if (!(auditorState.Value.CurrentState == State.Ready || auditorState.Value.CurrentState == State.Running))
                            continue;
                        if (Context.Constellation.Alpha == auditorState.Key)
                            isAlphaReady = true;
                        connectedCount++;
                    }
                    return isAlphaReady && Context.HasMajority(connectedCount, false);
                }
                else
                    //if auditor doesn't have connections with another auditors, we only need to verify alpha's state
                    return auditors.TryGetValue(Context.Constellation.Alpha, out var alphaState) && (alphaState.CurrentState == State.Ready || alphaState.CurrentState == State.Running);
            }
        }

        private void SetState(State state, Exception exc = null)
        {
            if (exc != null)
                logger.Error(exc);
            if (State != state)
            {
                logger.Info($"State update: new state: {state}, prev state: {State}");
                var stateArgs = new StateChangedEventArgs(state, State);
                State = state;

                StateChanged?.Invoke(stateArgs);
                var updateMessage = new StateUpdateMessage { State = State }
                    .CreateEnvelope<MessageEnvelopeSignless>()
                    .Sign(Context.Settings.KeyPair);
                Context.NotifyAuditors(updateMessage);
                logger.Info($"State update {state} sent.");
            }
        }


        public class AuditorState
        {
            public State? CurrentState { get; set; }

            public bool IsRunning => CurrentState == State.Running || CurrentState == State.Ready || CurrentState == State.Chasing;

            public bool IsWaitingForInit => CurrentState == State.Undefined;

            public bool IsReady => CurrentState == State.Ready;
        }
    }
}