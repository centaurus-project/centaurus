using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class RequestQuantaBatchHandler : MessageHandlerBase<IncomingAuditorConnection>
    {
        public RequestQuantaBatchHandler(ExecutionContext context)
            : base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(RequestQuantaBatch).Name;

        public override Task HandleMessage(IncomingAuditorConnection connection, IncomingMessage message)
        {
            var requests = (RequestQuantaBatch)message.Envelope.Message;
            if (Context.IsAlpha)
            {
                foreach (var request in requests.Requests)
                {
                    var requestQuantum = RequestQuantumHelper.GetQuantum(request);
                    Context.QuantumHandler.HandleAsync(requestQuantum, QuantumSignatureValidator.Validate(requestQuantum));
                }
            }
            else
            {
                Context.ProxyWorker.AddRequestsToQueue(requests.Requests.ToArray());
            }
            return Task.CompletedTask;
        }
    }
}