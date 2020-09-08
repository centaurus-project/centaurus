using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using stellar_dotnet_sdk;

namespace Centaurus.Domain
{

    public class WithdrawalProcessor : QuantumRequestProcessor<WithdrawalProcessorContext>
    {
        public override MessageTypes SupportedMessageType => MessageTypes.WithdrawalRequest;

        public override Task<ResultMessage> Process(WithdrawalProcessorContext context)
        {
            context.UpdateNonce();

            var requestQuantum = (RequestQuantum)context.Envelope.Message;

            var payment = (PaymentRequestBase)requestQuantum.RequestEnvelope.Message;

            var paymentAccount = payment.AccountWrapper.Account;

            var withdrawal = new Withdrawal
            {
                Apex = requestQuantum.Apex,
                Hash = context.TransactionHash,
                Source = payment.AccountWrapper,
                Withdrawals = context.WithdrawalItems,
                MaxTime = context.Transaction.TimeBounds.MaxTime
            };
            context.EffectProcessors.AddWithdrawalCreate(withdrawal, Global.WithdrawalStorage);

            foreach (var withdrawalItem in context.WithdrawalItems)
                context.EffectProcessors.AddLockLiabilities(paymentAccount, withdrawalItem.Asset, withdrawalItem.Amount);

            var effects = context.EffectProcessors.GetEffects();

            var accountEffects = effects.Where(e => ByteArrayPrimitives.Equals(e.Pubkey.Data, payment.Account.Data)).ToList();
            return Task.FromResult(context.Envelope.CreateResult(ResultStatusCodes.Success, accountEffects));
        }

        public override Task Validate(WithdrawalProcessorContext context)
        {
            context.ValidateNonce();
            ValidateWithdrawal(context);

            return Task.CompletedTask;
        }

        private void ValidateWithdrawal(WithdrawalProcessorContext context)
        {
            var payment = (context.Envelope.Message as RequestQuantum).RequestEnvelope.Message as PaymentRequestBase;
            if (payment == null)
                throw new InvalidOperationException("The quantum must be an instance of PaymentRequestBase");

            context.Transaction = payment.DeserializeTransaction();
            ValidateTransaction(context.Transaction);

            context.WithdrawalItems = context.Transaction.GetWithdrawals(payment.AccountWrapper.Account);
            if (context.WithdrawalItems.Count() < 1)
                throw new InvalidOperationException("No payment operations.");

            ValidateChangeTrustOperations(context.Transaction, context.WithdrawalItems);
        }

        private void ValidateTransaction(Transaction transaction)
        {
            var txSourceAccount = transaction.SourceAccount;
            if (ByteArrayPrimitives.Equals(Global.Constellation.Vault.Data, txSourceAccount.PublicKey))
                throw new InvalidOperationException("Vault account cannot be used as transaction source.");

            if (transaction.TimeBounds == null || transaction.TimeBounds.MaxTime <= 0)
                throw new InvalidOperationException("Max time must be set.");

            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (transaction.TimeBounds.MaxTime - currentTime > 1000)
                throw new InvalidOperationException("Transaction expiration time is to far.");
        }

        private void ValidateChangeTrustOperations(Transaction transaction, IEnumerable<WithdrawalItem> withdrawals)
        {
            var changeTrustOps = transaction.Operations
                .Where(o => o is ChangeTrustOperation)
                .Cast<ChangeTrustOperation>()
                .ToArray();

            foreach (var changeTrustOp in changeTrustOps)
            {
                if (changeTrustOp.SourceAccount != null) //allow change trustline only for tx source 
                    throw new InvalidOperationException("Change trustline operation only supported for transaction source account.");

                if (changeTrustOp.Asset is AssetTypeNative)
                    throw new InvalidOperationException("Change trustline operation only supported for non-native assets.");

                var limit = Amount.ToXdr(changeTrustOp.Limit);
                if (limit == 0)
                    throw new InvalidOperationException("Trustline deletion is not allowed.");

                if (!Global.Constellation.TryFindAssetSettings(changeTrustOp.Asset, out var asset))
                    throw new InvalidOperationException("Asset is not allowed by constellation.");
                var assetWithdrawal = withdrawals.FirstOrDefault(w => w.Asset == asset.Id);
                if (assetWithdrawal == null)
                    throw new InvalidOperationException("Change trustline operations allowed only for assets that are used in payments.");
                if (assetWithdrawal.Amount > limit)
                    throw new InvalidOperationException("Change trustline limit must be greater or equal to payment amount.");
            }

        }

        public override WithdrawalProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new WithdrawalProcessorContext(container);
        }
    }
}
