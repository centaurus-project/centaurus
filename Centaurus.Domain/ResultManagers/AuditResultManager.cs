using Centaurus.Models;
using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AuditResultManager : MajorityManager
    {
        public async Task Add(MessageEnvelope envelope)
        {
            await Aggregate(envelope);
        }

        protected override async Task OnResult(MajorityResults majorityResult, MessageEnvelope confirmation)
        {
            await base.OnResult(majorityResult, confirmation);
            if (majorityResult != MajorityResults.Success)
            {
                logger.Info($"Majority result received ({majorityResult}).");
                return;
            }

            var resultMessage = (ResultMessage)confirmation.Message;
            //we lost consensus
            if (resultMessage.Status != ResultStatusCodes.Success)
            {
                logger.Error("Result message status is not Success.");
                Global.AppState.State = ApplicationState.Failed;
                return;
            }

            ProccessResult(confirmation);
        }

        private void ProccessResult(MessageEnvelope confirmation)
        {
            var originalEnvelope = ((ResultMessage)confirmation.Message).OriginalMessage;
            NonceRequestMessage requestMessage = null;
            if (originalEnvelope.Message is NonceRequestMessage)
                requestMessage = (NonceRequestMessage)originalEnvelope.Message;
            else if (originalEnvelope.Message is RequestQuantum)
                requestMessage = ((RequestQuantum)originalEnvelope.Message).RequestEnvelope.Message as NonceRequestMessage;

            if (requestMessage != null)
                Notifier.Notify(requestMessage.Account, confirmation);
        }
        protected override byte[] GetHash(MessageEnvelope envelope)
        {
            return ((ResultMessage)envelope.Message).OriginalMessage.ComputeHash();
        }
    }
}
