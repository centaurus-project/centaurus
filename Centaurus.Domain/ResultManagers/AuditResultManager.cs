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

            if (TryGetITransactionContainer(confirmation, out var transactionContainer))
                SubmitTransaction(confirmation, transactionContainer);

            ProccessResult(confirmation);
        }

        private bool TryGetITransactionContainer(MessageEnvelope confirmation, out ITransactionContainer transactionContainer)
        {
            transactionContainer = null;
            var originalMessage = ((ResultMessage)confirmation.Message).OriginalMessage.Message;
            if (originalMessage is ITransactionContainer)
                transactionContainer = (ITransactionContainer)confirmation.Message;
            else if (originalMessage is RequestQuantum && ((RequestQuantum)originalMessage).RequestMessage is ITransactionContainer)
                transactionContainer = (ITransactionContainer)((RequestQuantum)originalMessage).RequestMessage;
            return transactionContainer != null && transactionContainer.HasTransaction();
        }

        private void SubmitTransaction(MessageEnvelope confirmation, ITransactionContainer transactionContainer)
        {
            try
            {
                //we have consensus
                var tx = transactionContainer.DeserializeTransaction();

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

                if (tx.Signatures.Count < majority)
                    throw new InvalidOperationException("Not enough signatures to match the threshold.");
                Global.WithdrawalStorage.Submit(tx.Hash(), tx);
            }
            catch (Exception e)
            {
                logger.Error(e);
                Global.AppState.State = ApplicationState.Failed;
            }
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
    }
}
