using Centaurus.Client;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Centaurus.Domain
{
    public class OutgoingConnectionManager : ContextualBase
    {
        private readonly Dictionary<RawPubKey, AtomicConnection> connections = new Dictionary<RawPubKey, AtomicConnection>();

        private OutgoingConnectionFactoryBase connectionFactory;

        public OutgoingConnectionManager(ExecutionContext context, OutgoingConnectionFactoryBase connectionFactory)
            : base(context)
        {
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException();
        }

        public void Connect(List<Settings.Auditor> auditors)
        {
            if (auditors == null)
                throw new ArgumentNullException(nameof(auditors));
            try
            {
                CloseInvalidConnections(auditors);

                foreach (var auditor in auditors)
                {
                    if (connections.ContainsKey(auditor.PubKey) || auditor.PubKey.Equals(Context.Settings.KeyPair))
                        continue;

                    var connection = new AtomicConnection(this, auditor.PubKey, GetCentaurusConnection(auditor, Context.Settings.UseSecureConnection));
                    connections.Add(auditor.PubKey, connection);
                    connection.Run();
                }
            }
            catch (Exception exc)
            {
                if (Context.StateManager != null)
                    Context.StateManager.Failed(exc);
            }
        }

        public void CloseAllConnections()
        {
            foreach (var connection in connections.Values)
                connection.Shutdown();
            connections.Clear();
        }

        private void CloseInvalidConnections(List<Settings.Auditor> auditors)
        {
            var connectionsToDrop = connections.Values
                .Where(c => !auditors.Any(a => a.PubKey.Equals(c.PubKey) && GetCentaurusConnection(a, Context.Settings.UseSecureConnection) == c.Address))
                .Select(c => c.PubKey)
                .ToList();
            foreach (var connectionKey in connectionsToDrop)
            {
                if (!connections.Remove(connectionKey, out var connection))
                    throw new Exception($"Unable to drop connection {connectionKey}");
                connection.Shutdown();
            }
        }

        public static Uri GetCentaurusConnection(Settings.Auditor auditor, bool useSecureConnection)
        {
            return new Uri(auditor.GetWsConnection(useSecureConnection), WebSocketConstants.CentaurusWebSocketEndPoint);
        }

        public void EnqueueResult(AuditorResultMessage result)
        {
            foreach (var connection in connections)
                connection.Value.OutgoingResultsStorage.EnqueueResult(result);
        }

        public void EnqueueMessage(MessageEnvelope message)
        {
            foreach (var connection in connections)
                connection.Value.OutgoingMessageStorage.EnqueueMessage(message);
        }

        class AtomicConnection : ContextualBase
        {

            public AtomicConnection(OutgoingConnectionManager connectionManager, RawPubKey keyPair, Uri address)
                : base(connectionManager?.Context)
            {
                PubKey = keyPair ?? throw new ArgumentNullException(nameof(keyPair));
                Address = address ?? throw new ArgumentNullException(nameof(address));

                this.connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
                OutgoingMessageStorage = new OutgoingMessageStorage();
                OutgoingResultsStorage = new OutgoingResultsStorage(OutgoingMessageStorage);
            }

            public OutgoingMessageStorage OutgoingMessageStorage { get; }

            public OutgoingResultsStorage OutgoingResultsStorage { get; }

            public void Run()
            {
                try
                {
                    ConnectToAuditor();
                }
                catch (Exception exc)
                {
                    logger.Error(exc);
                    throw;
                }
            }

            public void Shutdown()
            {
                lock (syncRoot)
                {
                    isAborted = true;
                    Unsubscribe(auditor);
                    CloseConnection(auditor).Wait();
                    OutgoingResultsStorage.Dispose();
                }
            }

            public RawPubKey PubKey { get; }

            public Uri Address { get; }

            private void ConnectToAuditor()
            {
                Task.Factory.StartNew(async () =>
                {
                    var uriBuilder = new UriBuilder(Address);
                    var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                    query[WebSocketConstants.PubkeyParamName] = Context.Settings.KeyPair.AccountId;
                    uriBuilder.Query = query.ToString();

                    var connectionUri = uriBuilder.Uri;

                    while (!isAborted)
                    {
                        var _auditor = new OutgoingConnection(Context, PubKey, OutgoingMessageStorage, connectionManager.connectionFactory.GetConnection());
                        try
                        {
                            Subscribe(_auditor);
                            await _auditor.EstablishConnection(connectionUri);
                            auditor = _auditor;
                            Context.StateManager.SetConnection(auditor);
                            break;
                        }
                        catch (Exception exc)
                        {
                            Unsubscribe(_auditor);
                            await CloseConnection(_auditor);

                            if (!(exc is OperationCanceledException))
                                logger.Info(exc, $"Unable establish connection with {connectionUri}. Retry in 5000ms");
                            Thread.Sleep(1000);
                        }
                    }
                });
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

            private void OnConnectionStateChanged((ConnectionBase connection, ConnectionState prev, ConnectionState current) args)
            {
                switch (args.current)
                {
                    case ConnectionState.Closed:
                        Close(args.connection);
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
                    Context.StateManager.RemoveConnection(_auditor);
                    _auditor.Dispose();
                }
            }

            private void Close(ConnectionBase e)
            {
                lock (syncRoot)
                {
                    Unsubscribe(auditor);
                    CloseConnection(auditor).Wait();
                    auditor = null;
                    if (!isAborted)
                        ConnectToAuditor();
                }
            }

            private readonly object syncRoot = new { };

            private OutgoingConnectionManager connectionManager;
            private bool isAborted = false;

            private OutgoingConnection auditor;

            private Logger logger = LogManager.GetCurrentClassLogger();
        }
    }
}
