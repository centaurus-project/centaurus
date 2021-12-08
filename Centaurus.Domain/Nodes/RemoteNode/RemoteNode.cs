using Centaurus.Domain.Quanta.Sync;
using Centaurus.Models;
using System;
using System.Collections.Generic;

namespace Centaurus.Domain.StateManagers
{
    internal partial class RemoteNode : NodeBase
    {
        public RemoteNode(ExecutionContext context, RawPubKey rawPubKey, Uri address)
            : base(context, rawPubKey)
        {
            Address = address;
        }

        private Uri address;
        public Uri Address
        {
            get => address;
            private set
            {
                if (value == address)
                    return;

                address = value;

                if (atomicConnection != null)
                {
                    atomicConnection.Shutdown();
                    atomicConnection = null;
                }
                if (value != null)
                    atomicConnection = new RemoteNodeConnection(this);
            }
        }

        private RemoteNodeConnection atomicConnection;

        public event Action<RemoteNode> OnStateUpdated;

        public event Action<RemoteNode> OnLastApexUpdated;

        public void RegisterIncomingConnection(IncomingNodeConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            IncomingConnection = connection;
        }

        public void RemoveIncomingConnection()
        {
            IncomingConnection = null;
            if (this.GetConnection() == null)
            {
                State = State.Undefined;
                cursors.Clear();
            }
        }

        public void Connect()
        {
            atomicConnection?.Run();
        }

        public void CloseOugoingConnection()
        {
            if (atomicConnection != null)
            {
                atomicConnection.Shutdown();
                atomicConnection = null;
            }
        }

        public void EnqueueResult(AuditorResult result)
        {
            atomicConnection?.OutgoingResultsStorage.EnqueueResult(result);
        }

        public void EnqueueMessage(MessageEnvelopeBase message)
        {
            atomicConnection?.OutgoingMessageStorage.EnqueueMessage(message);
        }

        public IncomingNodeConnection IncomingConnection { get; private set; }

        public OutgoingConnection OutgoingConnection => atomicConnection?.Connection;

        public List<RemoteNodeCursor> GetActiveCursors()
        {
            return cursors.GetActiveCursors();
        }

        public void SetCursor(SyncCursorType cursorType, DateTime timeToken, ulong currentCursor, bool force = false)
        {
            var cursor = cursors.Get(cursorType, true);
            cursor.SetCursor(currentCursor, timeToken, force);
        }

        public void DisableSync(SyncCursorType cursorType)
        {
            var cursor = cursors.Get(cursorType);
            cursor?.DisableSync();
        }

        public void Update(StateMessage stateMessage)
        {
            lock (syncRoot)
            {
                //skip old data
                if (UpdateDate > stateMessage.UpdateDate)
                    return;
                SetState(stateMessage);
                SetApex(stateMessage.UpdateDate, stateMessage.CurrentApex);
                SetQuantaQueueLength(stateMessage.UpdateDate, stateMessage.QuantaQueueLength);
                LastPersistedApex = stateMessage.LastPersistedApex;
                UpdateDate = stateMessage.UpdateDate;
            }
        }

        protected override void SetApex(DateTime updateDate, ulong apex)
        {
            var lastApex = LastApex;
            base.SetApex(updateDate, apex);
            if (lastApex != LastApex)
                OnLastApexUpdated?.Invoke(this);
        }

        private void SetState(StateMessage stateMessage)
        {
            if (State != stateMessage.State)
            {
                State = stateMessage.State;
                OnStateUpdated?.Invoke(this);
            }
        }

        private object syncRoot = new { };

        private SyncCursorCollection cursors = new SyncCursorCollection();
    }
}