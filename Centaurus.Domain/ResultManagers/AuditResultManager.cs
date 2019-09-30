using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public class AuditResultManager : MajorityManager
    {
        public MessageEnvelope Add(MessageEnvelope envelope)
        {
            var confirmation = Aggregate(envelope);
            if (confirmation == null)
                return null;
            if (!(confirmation.Message is ITransactionContainer))
                return confirmation;

            //we have consensus
            var tx = ((ITransactionContainer)confirmation.Message).GetTransaction();

            int majority = MajorityHelper.GetMajorityCount();

            var resultMessage = ((ResultMessage)confirmation.Message);
            
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
            var _ = Global.StellarNetwork.Server.SubmitTransaction(tx).ContinueWith(response =>
            {
                //TODO: cleanup this mess
                if (!response.Result.IsSuccess())
                    throw new Exception("Failed to send transaction");
            });

            return confirmation;
        }
    }
}
