using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;

namespace Centaurus.Domain.Handlers.AlphaHandlers
{
    public class AlphaResultMessageHandler : BaseAlphaMessageHandler
    {
        public override MessageTypes SupportedMessageType { get; } = MessageTypes.ResultMessage;

        public override ConnectionState[] ValidConnectionStates { get; } = new ConnectionState[] { ConnectionState.Ready };

        public override bool IsAuditorOnly { get; } = true;

        public override Task HandleMessage(AlphaWebSocketConnection connection, MessageEnvelope envelope)
        {
            var aggregationResult = Global.AuditResultManager.Add(envelope);
            if (aggregationResult == null)
                return Task.CompletedTask;

            //we lost consensus
            if (((ResultMessage)aggregationResult.Message).Status != ResultStatusCodes.Success)
            {
                Global.AppState.State = ApplicationState.Failed;
            }


            var resultMessage = (ResultMessage)envelope.Message;
            if (resultMessage.OriginalMessage.Message is SnapshotQuantum)
            {
                Global.SnapshotManager.SetResult(aggregationResult);
                return Task.CompletedTask;
            }

            RequestMessage requestMessage = null;
            if (aggregationResult.Message is RequestMessage)
                requestMessage = (RequestMessage)aggregationResult.Message;
            else if (aggregationResult.Message is RequestQuantum)
            {
                requestMessage = ((RequestQuantum)aggregationResult.Message).RequestEnvelope.Message as RequestMessage;
            }

            if (requestMessage != null)
                Notifier.Notify(requestMessage.Account, aggregationResult);
            return Task.CompletedTask;
        }
    }
}