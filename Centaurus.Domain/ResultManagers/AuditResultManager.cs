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

            if (TryGetWithdrawal(confirmation, out var withdrawal))
                SubmitTransaction(confirmation, withdrawal);

            ProccessResult(confirmation);
        }

        private bool TryGetWithdrawal(MessageEnvelope confirmation, out WithdrawalRequest withdrawal)
        {
            withdrawal = null;
            var originalMessage = ((ResultMessage)confirmation.Message).OriginalMessage.Message;
            if (originalMessage is RequestQuantum && ((RequestQuantum)originalMessage).RequestMessage is WithdrawalRequest)
                withdrawal = (WithdrawalRequest)((RequestQuantum)originalMessage).RequestMessage;
            return withdrawal != null;
        }

        private void SubmitTransaction(MessageEnvelope confirmation, WithdrawalRequest withdrawal)
        {
            try
            {
                //we have consensus
                var tx = withdrawal.DeserializeTransaction();

                int majority = MajorityHelper.GetMajorityCount();

                var resultMessage = (ResultMessage)confirmation.Message;

                var transactionEffects = resultMessage.Effects
                    .Where(e => e is TransactionSignedEffect)
                    .Cast<TransactionSignedEffect>()
                    .ToArray();
                var signatures = new List<DecoratedSignature>();
                for (var i = 0; i < transactionEffects.Length && tx.Signatures.Count <= majority; i++)
                {
                    var effect = transactionEffects[i];
                    var signature = effect.Signature.ToDecoratedSignature();
                    //TODO: verify the signature here and check that it is unique
                    signatures.Add(signature);
                }

                if (tx.Signatures.Count < majority)
                    throw new InvalidOperationException("Not enough signatures to match the threshold.");
                Global.WithdrawalStorage.AssignSignatures(tx.Hash(), signatures);
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
