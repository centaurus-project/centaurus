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

            context.EffectProcessors.AddWithdrawalCreate(context.Withdrawal, context.PaymentsManager.WithdrawalStorage);

            var effects = context.EffectProcessors.Effects;

            var accountEffects = effects.Where(e => e.AccountWrapper?.Account.Id == context.WithdrawalRequest.Account).ToList();
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

            context.PaymentsManager.PaymentsParser.ValidateTransaction(context.Transaction, context.PaymentsManager.Vault);

            context.Withdrawal = context.PaymentsManager.PaymentsParser.GetWithdrawal(context.Envelope, context.Transaction, context.CentaurusContext.Constellation, context.PaymentsManager.Vault);

            if (context.Withdrawal.Items.Count() < 1)
                throw new BadRequestException("No payment operations.");
        }

        public override WithdrawalProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new WithdrawalProcessorContext(container);
        }
    }
}