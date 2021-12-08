using Centaurus.Models;
using NLog;
using System;

namespace Centaurus.Domain.StateManagers
{
    internal class CurrentNode : NodeBase
    {
        public CurrentNode(ExecutionContext context, RawPubKey rawPubKey, State initState)
            :base(context, rawPubKey)
        {
            State = initState;
        }

        public event Action<StateChangedEventArgs> StateChanged;

        public void Init(State state)
        {
            lock (syncRoot)
            {
                if (State != State.WaitingForInit)
                    throw new InvalidOperationException("Context is already initialized.");

                if (state == State.WaitingForInit || state == State.Undefined)
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

        public void Rised()
        {
            lock (syncRoot)
                UpdateState(State.Running);
        }

        public void RefreshState()
        {
            UpdateState(State);
        }

        public void UpdateData(ulong currentApex, ulong lastPersistedApex, int quantaQueueLenght, DateTime updateDate)
        {
            lock (syncRoot)
            {
                SetApex(updateDate, currentApex);
                SetQuantaQueueLength(updateDate, quantaQueueLenght);
                LastPersistedApex = lastPersistedApex;
                UpdateDelay();
                UpdateDate = updateDate;
            }
        }

        /// <summary>
        /// Updates current node delay
        /// </summary>
        private void UpdateDelay()
        {
            if (Context.IsAlpha)
                return;
            lock (syncRoot)
            {
                var isDelayed = State.Chasing == State;
                var currentApex = Context.QuantumHandler.CurrentApex;
                var syncApex = Context.NodesManager.SyncSource.LastApex;
                if (isDelayed)
                {
                    if (syncApex <= currentApex || syncApex - currentApex < RunningDelayTreshold)
                        UpdateState(State.Running);
                    logger.Info($"Current node delay is {syncApex - currentApex}");
                }
                else
                {
                    if (syncApex > currentApex && syncApex - currentApex > ChasingDelayTreshold)
                    {
                        logger.Info($"Current node delay is {syncApex - currentApex}");
                        UpdateState(State.Chasing);
                    }
                }
            }
        }

        private const ulong ChasingDelayTreshold = 50_000;
        private const ulong RunningDelayTreshold = 10_000;
        private object syncRoot = new { };
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private void UpdateState(State state)
        {
            lock (syncRoot)
            {
                if (state == State.Running && Context.NodesManager.IsMajorityReady)
                    SetState(State.Ready);
                else if (state == State.Ready && Context.NodesManager.IsMajorityReady)
                    SetState(State.Running);
                else
                    SetState(state);
            }
        }

        private void SetState(State state, Exception exc = null)
        {
            if (exc != null)
                logger.Error(exc);
            if (State != state)
            {
                logger.Info($"State update: new state: {state}, prev state: {State}");
                var stateArgs = new StateChangedEventArgs(this, state, State);
                State = state;

                StateChanged?.Invoke(stateArgs);
                UpdateDate = DateTime.UtcNow;
            }
        }
    }
}
