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
    internal class ProxyWorker : ContextualBase, IDisposable
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        public ProxyWorker(ExecutionContext context)
            : base(context)
        {
            cancellationTokenSource = new CancellationTokenSource();
            Run();
        }

        private void Run()
        {
            Task.Factory.StartNew(async () =>
            {
                var hasQuantaToSend = false;
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    var requestsToSend = default(List<MessageEnvelopeBase>);
                    try
                    {
                        if (!hasQuantaToSend)
                        {
                            Thread.Sleep(20);
                        }
                        lock (quantaCollectionSyncRoot)
                        {
                            if (requests.Count > 0)
                            {
                                requestsToSend = requests
                                    .Take(500)
                                    .ToList();
                                requests.RemoveRange(0, requestsToSend.Count);
                            }
                        }
                        hasQuantaToSend = requestsToSend != null;
                        if (hasQuantaToSend)
                        {
                            if (!Context.NodesManager.TryGetNode(Context.Constellation.Alpha, out var node))
                                throw new Exception($"Unable to get Alpha node.");
                            var connection = node.GetConnection();
                            if (connection == null)
                                throw new Exception($"Alpha node is not connected.");
                            await connection.SendMessage(new RequestQuantaBatch { Requests = requestsToSend }.CreateEnvelope<MessageEnvelopeSignless>());
                        }
                    }
                    catch (Exception exc)
                    {
                        logger.Error(exc, "Error on sending quanta requests to Alpha.");
                        if (requestsToSend != null)
                            foreach (var request in requestsToSend)
                            {
                                var requestMessage = (RequestMessage)request.Message;
                                Notifier.Notify(Context, requestMessage.Account, request.CreateResult(ResultStatusCode.InvalidState).CreateEnvelope());
                            }
                    }
                }
            });
        }

        public void SetAuditorConnection(OutgoingConnection alpha)
        {
            AlphaConnection = alpha ?? throw new ArgumentNullException(nameof(alpha));
        }

        private object quantaCollectionSyncRoot = new { };
        private List<MessageEnvelopeBase> requests = new List<MessageEnvelopeBase>();
        private CancellationTokenSource cancellationTokenSource;

        public OutgoingConnection AlphaConnection { get; private set; }

        public void AddRequestsToQueue(params MessageEnvelopeBase[] clientRequests)
        {
            if (!Context.IsAlpha)
                lock (quantaCollectionSyncRoot)
                    requests.AddRange(clientRequests);
            //TODO: add to QuantumHandler
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}
