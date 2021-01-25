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

            var withdrawal = new WithdrawalWrapper
            {
                Envelope = context.Envelope,
                Hash = context.TransactionHash,
                Withdrawals = context.WithdrawalItems,
                MaxTime = context.Transaction.TimeBounds.MaxTime
            };
            context.EffectProcessors.AddWithdrawalCreate(withdrawal, Global.WithdrawalStorage);

            foreach (var withdrawalItem in context.WithdrawalItems)
                context.EffectProcessors.AddUpdateLiabilities(context.WithdrawalRequest.AccountWrapper.Account, withdrawalItem.Asset, withdrawalItem.Amount);

            var effects = context.EffectProcessors.Effects;

            var accountEffects = effects.Where(e => e.Account == context.WithdrawalRequest.Account).ToList();
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
            if (context.Request.RequestMessage.AccountWrapper.HasPendingWithdrawal)
                throw new BadRequestException("Withdrawal already exists.");

            context.Transaction = context.WithdrawalRequest.DeserializeTransaction();
            ValidateTransaction(context.Transaction);

            context.WithdrawalItems = context.Transaction.GetWithdrawals(context.WithdrawalRequest.AccountWrapper.Account, Global.Constellation);
            if (context.WithdrawalItems.Count() < 1)
                throw new BadRequestException("No payment operations.");
        }

        private void ValidateTransaction(Transaction transaction)
        {
            var txSourceAccount = transaction.SourceAccount;
            if (ByteArrayPrimitives.Equals(Global.Constellation.Vault.Data, txSourceAccount.PublicKey))
                throw new BadRequestException("Vault account cannot be used as transaction source.");

            if (transaction.TimeBounds == null || transaction.TimeBounds.MaxTime <= 0)
                throw new BadRequestException("Max time must be set.");

            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (transaction.TimeBounds.MaxTime - currentTime > 1000)
                throw new BadRequestException("Transaction expiration time is to far.");

            if (transaction.Operations.Any(o => !(o is PaymentOperation)))
                throw new BadRequestException("Only payment operations are allowed.");

            if (transaction.Operations.Length > 100)
                throw new BadRequestException("Too many operations.");
        }

        public override WithdrawalProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new WithdrawalProcessorContext(container);
        }
    }
}
