using Centaurus.Domain.Nodes.Common;
using Centaurus.Domain.Quanta.Sync;
using Centaurus.Domain.RemoteNodes;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    internal partial class RemoteNode : NodeBase
    {
        public RemoteNode(ExecutionContext context, RawPubKey rawPubKey)
            : base(context, rawPubKey)
        {
            connectionManager = new RemoteNodeConnectionManager(this);
            connectionManager.OnConnected += ConnectionManager_OnConnected;
            connectionManager.OnConnectionClosed += ConnectionManager_OnConnectionClosed;
        }

        private void ConnectionManager_OnConnectionClosed()
        {
            State = State.Undefined;
            cursors.Clear();
        }

        private void ConnectionManager_OnConnected()
        {
            //subscribe for the current remote node signatures
            if (Context.NodesManager.CurrentNode.IsPrimeNode)
            {
                var connection = connectionManager.GetConnection();
                _ = connection.SendMessage(new SyncCursorReset
                {
                    Cursors = new List<SyncCursor> {
                        new SyncCursor {
                            Type = XdrSyncCursorType.SingleNodeSignatures,
                            Cursor = Context.PendingUpdatesManager.LastPersistedApex }
                    }
                });
            }
        }

        //TODO: find a way to verify that node is Prime
        public override bool IsPrimeNode => Address != null;

        public Uri Address => Settings.Address;

        public bool IsConnected => connectionManager.IsConnected;

        public event Action<RemoteNode> OnStateUpdated;

        public event Action<RemoteNode> OnLastApexUpdated;

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
                var updateDate = new DateTime(stateMessage.UpdateDate);
                if (UpdateDate > updateDate)
                    return;
                SetState(stateMessage);
                SetApex(updateDate, stateMessage.CurrentApex);
                SetQuantaQueueLength(updateDate, stateMessage.QuantaQueueLength);
                LastPersistedApex = stateMessage.LastPersistedApex;
                UpdateDate = updateDate;
            }
        }

        public ConnectionBase GetConnection()
        {
            return connectionManager.GetConnection();
        }

        public void RegisterIncomingConnection(IncomingNodeConnection auditorConnection)
        {
            connectionManager.RegisterIncomingConnection(auditorConnection);
        }

        public void RemoveIncomingConnection()
        {
            connectionManager.RemoveIncomingConnection();
        }

        public async Task CloseOutConnection()
        {
            await connectionManager.CloseOutConnection();
        }

        public void ConnectTo()
        {
            connectionManager.ConnectTo();
        }

        public async Task CloseConnections()
        {
            await connectionManager.CloseConnections();
        }

        private void SetState(StateMessage stateMessage)
        {
            if (State != stateMessage.State)
            {
                State = stateMessage.State;
                OnStateUpdated?.Invoke(this);
            }
        }

        protected override void SetApex(DateTime updateDate, ulong apex)
        {
            var lastApex = LastApex;
            base.SetApex(updateDate, apex);
            if (lastApex != LastApex)
                OnLastApexUpdated?.Invoke(this);
        }

        private object syncRoot = new { };

        private SyncCursorCollection cursors = new SyncCursorCollection();
        private RemoteNodeConnectionManager connectionManager;
    }
}