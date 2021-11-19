using Centaurus.Client;
using Centaurus.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Centaurus.Domain
{
    public class OutgoingConnectionManager : ContextualBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

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
                    if (!auditor.IsPrime //not prime server
                        || connections.ContainsKey(auditor.PubKey) //already connected
                        || auditor.PubKey.Equals(Context.Settings.KeyPair)) //current server
                        continue;

                    var connection = new AtomicConnection(this, auditor.PubKey, GetAuditorUri(auditor, Context.Settings.UseSecureConnection));
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

        public async Task SendToAll(MessageEnvelopeBase message)
        {
            var sendTasks = new List<Task>();
            foreach (var c in connections)
            {
                sendTasks.Add(c.Value.SendMessage(message));
            }
            await Task.WhenAll(sendTasks);
        }

        public async Task SendTo(RawPubKey rawPubKey, MessageEnvelopeBase message)
        {
            if (!connections.TryGetValue(rawPubKey, out var connection))
                throw new Exception($"Connection {rawPubKey} is not found.");
            await connection.SendMessage(message);
        }

        public void CloseAllConnections()
        {
            foreach (var connection in connections.Values)
                connection.Shutdown();
            connections.Clear();
        }

        private void CloseInvalidConnections(List<Settings.Auditor> auditors)
        {
            var connectionsToDrop = new List<RawPubKey>();
            foreach (var connection in connections.Values)
            {
                var auditor = auditors.FirstOrDefault(a => a.PubKey.Equals(connection.PubKey));
                if (auditor == null //not in address book
                    || !auditor.IsPrime //not prime server
                    || GetAuditorUri(auditor, Context.Settings.UseSecureConnection) != connection.Address) //address changed
                    connectionsToDrop.Add(connection.PubKey);
            }
            foreach (var connectionKey in connectionsToDrop)
            {
                if (!connections.Remove(connectionKey, out var connection))
                    throw new Exception($"Unable to drop connection {connectionKey}");
                connection.Shutdown();
            }
        }

        public static Uri GetAuditorUri(Settings.Auditor auditor, bool useSecureConnection)
        {
            return new Uri(auditor.GetWsConnection(useSecureConnection), WebSocketConstants.CentaurusWebSocketEndPoint);
        }

        public void EnqueueResult(AuditorResult result)
        {
            foreach (var connection in connections)
                connection.Value.OutgoingResultsStorage.EnqueueResult(result);
        }

        public void EnqueueMessage(MessageEnvelopeBase message)
        {
            foreach (var connection in connections)
            {
                connection.Value.OutgoingMessageStorage.EnqueueMessage(message);
            }
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
                    auditor?.CloseConnection();
                    OutgoingResultsStorage.Dispose();
                }
            }

            public async Task SendMessage(MessageEnvelopeBase message)
            {
                if (auditor != null)
                    await auditor.SendMessage(message);
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
                    var connectionAttempts = 0;
                    while (!isAborted)
                    {
                        if (Context.StateManager.State == State.Rising) //wait while all pending quanta will be handled
                        {
                            Thread.Sleep(1000);
                            continue;
                        }
                        auditor = new OutgoingConnection(Context, PubKey, OutgoingMessageStorage, connectionManager.connectionFactory.GetConnection());
                        try
                        {
                            await auditor.EstablishConnection(connectionUri);
                            await auditor.Listen();
                        }
                        catch (Exception exc)
                        {

                            if (!(exc is OperationCanceledException) && connectionAttempts % 100 == 0)
                                logger.Error(exc, $"Unable establish connection with {connectionUri} after {connectionAttempts} attempts. Retry in 1000ms");
                            Thread.Sleep(1000);
                            connectionAttempts++;
                        }
                        finally
                        {
                            auditor.Dispose();
                            auditor = null;
                        }
                    }
                });
            }

            private readonly object syncRoot = new { };

            private OutgoingConnectionManager connectionManager;
            private bool isAborted = false;

            private OutgoingConnection auditor;

            private Logger logger = LogManager.GetCurrentClassLogger();
        }
    }
}
