using Centaurus.Models;
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

            if (confirmation.Message is ITransactionContainer)
                await SubmitTransaction(confirmation);

            ProccessResult(confirmation);
        }

        private async Task SubmitTransaction(MessageEnvelope confirmation)
        {
            try
            {
                //we have consensus
                var tx = ((ITransactionContainer)confirmation.Message).GetTransaction();

                int majority = MajorityHelper.GetMajorityCount();

                var resultMessage = (ResultMessage)confirmation.Message;

                var transactionEffects = resultMessage.Effects
                    .Where(e => e is TransactionSignedEffect)
                    .Cast<TransactionSignedEffect>()
                    .ToArray();

                for (var i = 0; i < transactionEffects.Length && tx.Signatures.Count <= majority; i++)
                {
                    var effect = transactionEffects[i];
                    var signature = effect.Signature.ToDecoratedSignature();
                    //TODO: verify the signature here and check that it is unique
                    tx.Signatures.Add(signature);
                }

                if (tx.Signatures.Count <= majority)
                    throw new InvalidOperationException("Not enough signatures to match the threshold.");
                var result = await Global.StellarNetwork.Server.SubmitTransaction(tx);
                //TODO: cleanup this mess
                if (!result.IsSuccess())
                    throw new Exception("Failed to submit transaction. Result is: " + result.ResultXdr);
            }
            catch (Exception e)
            {
                logger.Error(e);
                Global.AppState.State = ApplicationState.Failed;
            }
        }

        private void ProccessResult(MessageEnvelope confirmation)
        {
            var resultMessage = (ResultMessage)confirmation.Message;
            if (resultMessage.OriginalMessage.Message is SnapshotQuantum)
            {
                Global.SnapshotManager.SetResult(confirmation);
                return;
            }

            RequestMessage requestMessage = null;
            if (confirmation.Message is RequestMessage)
                requestMessage = (RequestMessage)confirmation.Message;
            else if (confirmation.Message is RequestQuantum)
                requestMessage = ((RequestQuantum)confirmation.Message).RequestEnvelope.Message as RequestMessage;

            if (requestMessage != null)
                Notifier.Notify(requestMessage.Account, confirmation);
        }
    }
}
