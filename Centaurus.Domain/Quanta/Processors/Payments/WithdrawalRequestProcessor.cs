using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Centaurus.Models;
using stellar_dotnet_sdk;

namespace Centaurus.Domain
{

    public class WithdrawalProcessor : QuantumProcessor<WithdrawalProcessorContext>
    {
        public override MessageTypes SupportedMessageType => MessageTypes.WithdrawalRequest;

        public override Task<QuantumResultMessage> Process(WithdrawalProcessorContext context)
        {
            context.UpdateNonce();

            context.EffectProcessors.AddWithdrawalCreate(context.Withdrawal, context.PaymentProvider.WithdrawalStorage);

            return Task.FromResult((QuantumResultMessage)context.Envelope.CreateResult(ResultStatusCodes.Success));
        }

        public override Task Validate(WithdrawalProcessorContext context)
        {
            context.ValidateNonce();
            ValidateWithdrawal(context);

            return Task.CompletedTask;
        }

        private void ValidateWithdrawal(WithdrawalProcessorContext context)
        {
            if (context.SourceAccount.HasPendingWithdrawal)
                throw new BadRequestException("Withdrawal already exists.");

            context.PaymentProvider.ValidateTransaction(context.Transaction);
            context.Withdrawal = context.PaymentProvider.GetWithdrawal(context.Envelope, context.SourceAccount, context.Transaction);

            var sourceAccount = context.SourceAccount.Account;

            foreach (var withdrawalItem in context.Withdrawal.Items)
            {
                var centaurusAsset = context.CentaurusContext.Constellation.Assets.FirstOrDefault(a => a.Id == withdrawalItem.Asset);
                if (withdrawalItem.Asset == 0 && withdrawalItem.Amount < context.CentaurusContext.Constellation.MinAccountBalance)
                    throw new BadRequestException($"Min account balance is {context.CentaurusContext.Constellation.MinAccountBalance}.");

                if (!(sourceAccount.GetBalance(withdrawalItem.Asset)?.HasSufficientBalance(withdrawalItem.Amount) ?? false))
                    throw new BadRequestException($"Insufficient balance.");
                if (context.Withdrawal.Items.Count() < 1)
                    throw new BadRequestException("No payment operations.");
            }
        }

        public override WithdrawalProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new WithdrawalProcessorContext(container);
        }
    }
}