using Centaurus.Client;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class OutgoingConnectionManager : ContextualBase
    {
        private readonly Dictionary<RawPubKey, AtomicConnection> connections = new Dictionary<RawPubKey, AtomicConnection>();

        private Logger logger = LogManager.GetCurrentClassLogger();

        private OutgoingConnectionFactoryBase connectionFactory;

        public OutgoingConnectionManager(ExecutionContext context, OutgoingConnectionFactoryBase connectionFactory)
            : base(context)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException();
        }

        public async Task Connect()
        {
            try
            {
                await CloseInvalidConnections();
                foreach (var auditor in Context.Constellation.Auditors)
                {
                    if (connections.ContainsKey(auditor.PubKey) || auditor.PubKey == (RawPubKey)Context.Settings.KeyPair)
                        continue;
                    var connection = new AtomicConnection(this, auditor.PubKey, auditor.Address);
                    connections.Add(auditor.PubKey, connection);
                    _ = connection.Run();
                }
            }
            catch (Exception exc)
            {
                if (Context.AppState != null)
                    Context.AppState.SetState(State.Failed, exc);
            }
        }

        public async Task CloseAllConnections()
        {
            foreach (var connection in connections.Values)
                await connection.Shutdown();
            connections.Clear();
        }

        private async Task CloseInvalidConnections()
        {
            var connectionsToDrop = connections.Values
                .Where(c => !Context.Constellation.Auditors.Any(a => a.PubKey == c.PubKey && a.Address == c.Address))
                .Select(c => c.PubKey)
                .ToList();
            foreach (var connectionKey in connectionsToDrop)
            {
                if (!connections.Remove(connectionKey, out var connection))
                    throw new Exception($"Unable to drop connection {connectionKey}");
                await connection.Shutdown();
            }
        }

        class AtomicConnection : ContextualBase
        {

            public AtomicConnection(OutgoingConnectionManager auditorStartup, RawPubKey keyPair, string address)
                : base(auditorStartup?.Context)
            {
                PubKey = keyPair ?? throw new ArgumentNullException(nameof(keyPair));
                Address = address ?? throw new ArgumentNullException(nameof(address));

                this.connectionManager = auditorStartup ?? throw new ArgumentNullException(nameof(auditorStartup));

                if (!Uri.TryCreate(address, UriKind.Absolute, out connectionUri))
                    throw new ArgumentException($"{address} is not valid uri.");
            }

            public async Task Run()
            {
                try
                {
                    await ConnectToAuditor();
                }
                catch (Exception exc)
                {
                    logger.Error(exc);
                    throw;
                }
            }

            public async Task Shutdown()
            {
                await syncRoot.WaitAsync();
                try
                {
                    isAborted = true;
                    Unsubscribe(auditor);
                    CloseConnection(auditor).Wait();
                }
                finally
                {
                    syncRoot.Release();
                }
            }

            public RawPubKey PubKey { get; }

            public string Address { get; }

            private async Task ConnectToAuditor()
            {
                while (!isAborted)
                {
                    var _auditor = new OutgoingConnection(Context, PubKey, connectionManager.connectionFactory.GetConnection());
                    try
                    {
                        Subscribe(_auditor);
                        await _auditor.EstablishConnection(connectionUri);
                        auditor = _auditor;
                        Context.AppState.RegisterConnection(auditor);
                        break;
                    }
                    catch (Exception exc)
                    {
                        Unsubscribe(_auditor);
                        await CloseConnection(_auditor);

                        if (!(exc is OperationCanceledException))
                            logger.Info(exc, $"Unable establish connection with {connectionUri}. Retry in 5000ms");
                        Thread.Sleep(5000);
                    }
                }
            }

            private void Subscribe(OutgoingConnection _auditor)
            {
                if (_auditor != null)
                    _auditor.OnConnectionStateChanged += OnConnectionStateChanged;
            }

            private void Unsubscribe(OutgoingConnection _auditor)
            {
                if (_auditor != null)
                    _auditor.OnConnectionStateChanged -= OnConnectionStateChanged;
            }

            private async void OnConnectionStateChanged((ConnectionBase connection, ConnectionState prev, ConnectionState current) args)
            {
                switch (args.current)
                {
                    case ConnectionState.Closed:
                        await Close(args.connection);
                        break;
                    default:
                        break;
                }
            }

            private async Task CloseConnection(OutgoingConnection _auditor)
            {
                if (_auditor != null)
                {
                    await _auditor.CloseConnection();
                    Context.AppState.RemoveConnection(auditor);
                    _auditor.Dispose();
                }
            }

            private async Task Close(ConnectionBase e)
            {
                await syncRoot.WaitAsync();
                try
                {
                    Unsubscribe(auditor);
                    CloseConnection(auditor).Wait();
                    auditor = null;
                    if (!isAborted)
                        _ = ConnectToAuditor();
                }
                finally
                {
                    syncRoot.Release();
                }
            }

            private readonly SemaphoreSlim syncRoot = new SemaphoreSlim(1);

            private Uri connectionUri;
            private OutgoingConnectionManager connectionManager;
            private bool isAborted = false;

            private OutgoingConnection auditor;

            private Logger logger = LogManager.GetCurrentClassLogger();
        }
    }
}
