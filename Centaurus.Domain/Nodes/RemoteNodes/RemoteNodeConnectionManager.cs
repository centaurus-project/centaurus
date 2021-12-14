using System;
using System.Threading.Tasks;

namespace Centaurus.Domain.RemoteNodes
{
    internal class RemoteNodeConnectionManager
    {
        public RemoteNodeConnectionManager(RemoteNode remoteNode)
        {
            node = remoteNode;
        }

        public event Action OnConnectionClosed;
        public event Action OnConnected;

        public void ConnectTo()
        {
            if (atomicConnection != null)
            {
                throw new InvalidOperationException("Already connected.");
            }
            if (node.Address != null)
            {
                atomicConnection = new RemoteNodeConnection(node);
                atomicConnection.OnConnected += AtomicConnection_OnConnected;
                atomicConnection.OnConnectionClosed += AtomicConnection_OnConnectionClosed;
                atomicConnection.Run();
            }
        }

        public ConnectionBase GetConnection()
        {
            if (IncomingConnection?.IsAuthenticated ?? false)
                return IncomingConnection;
            return OutgoingConnection?.IsAuthenticated ?? false ? OutgoingConnection : null;
        }

        private void AtomicConnection_OnConnected()
        {
            OutConnected();
        }

        private void AtomicConnection_OnConnectionClosed()
        {
            OutConnectionClosed();
        }

        public async Task CloseInConnection()
        {
            if (IncomingConnection != null)
                await IncomingConnection.CloseConnection();
        }

        public async Task CloseConnections()
        {
            await Task.WhenAll(
                CloseOutConnection(),
                CloseInConnection()
            );
        }

        public async Task CloseOutConnection()
        {
            if (atomicConnection != null)
            {
                await atomicConnection.Shutdown();
                atomicConnection.OnConnected -= AtomicConnection_OnConnected;
                atomicConnection.OnConnectionClosed -= AtomicConnection_OnConnectionClosed;
                atomicConnection = null;
            }
        }

        private void OutConnected()
        {
            lock (syncRoot)
            {
                IsOutgoningConnectionEstablished = true;
                if (!IsConnected)
                    IsConnected = true;
            }
        }

        private void OutConnectionClosed()
        {
            lock (syncRoot)
            {
                if (!IsOutgoningConnectionEstablished)
                    return;
                IsOutgoningConnectionEstablished = false;
                if (IsConnected && !IsIncomingConnectionEstablished)
                    IsConnected = false;
            }
        }

        public void RegisterIncomingConnection(IncomingNodeConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            lock (syncRoot)
            {
                IncomingConnection = connection;
                if (!IsConnected)
                    IsConnected = true;
            }
        }

        public void RemoveIncomingConnection()
        {
            lock (syncRoot)
            {
                IncomingConnection = null;
                if (IsConnected && !IsOutgoningConnectionEstablished)
                    IsConnected = false;
            }
        }

        public IncomingNodeConnection IncomingConnection { get; private set; }

        public OutgoingConnection OutgoingConnection => atomicConnection?.Connection;

        private bool isConnected;
        public bool IsConnected
        {
            get => isConnected;
            private set
            {
                if (isConnected == value)
                    return;

                isConnected = value;
                if (isConnected)
                    OnConnected?.Invoke();
                else
                    OnConnectionClosed?.Invoke();
            }
        }

        public bool IsOutgoningConnectionEstablished { get; private set; }

        public bool IsIncomingConnectionEstablished { get; private set; }

        private RemoteNodeConnection atomicConnection;
        private RemoteNode node;
        private object syncRoot = new { };
    }
}
