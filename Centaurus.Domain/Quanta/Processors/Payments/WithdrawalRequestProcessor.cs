using Centaurus.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{

    public class WithdrawalProcessor : QuantumProcessor<WithdrawalProcessorContext>
    {
        public override MessageTypes SupportedMessageType => MessageTypes.WithdrawalRequest;

        public override Task<QuantumResultMessage> Process(WithdrawalProcessorContext context)
        {
            context.UpdateNonce();

            context.EffectProcessors.AddBalanceUpdate(context.SourceAccount, context.WithdrawalRequest.Asset, -context.WithdrawalRequest.Amount);

            return Task.FromResult((QuantumResultMessage)context.Envelope.CreateResult(ResultStatusCodes.Success));
        }

        public override Task Validate(WithdrawalProcessorContext context)
        {
            context.ValidateNonce();

            var sourceAccount = context.SourceAccount.Account;

            var centaurusAsset = context.CentaurusContext.Constellation.Assets.FirstOrDefault(a => a.Id == context.WithdrawalRequest.Asset);
            if (centaurusAsset == null || centaurusAsset.IsSuspended)
                throw new BadRequestException($"Constellation doesn't support asset '{context.WithdrawalRequest.Asset}'.");

            var providerAsset = context.PaymentProvider.Settings.Assets.FirstOrDefault(a => a.CentaurusAsset == context.WithdrawalRequest.Asset);
            if (providerAsset == null)
                throw new BadRequestException($"Current provider doesn't support withdrawal of asset {centaurusAsset.Code}.");

            var minBalance = centaurusAsset.Id == 0 ? context.CentaurusContext.Constellation.MinAccountBalance : 0;
            if (!(sourceAccount.GetBalance(centaurusAsset.Id)?.HasSufficientBalance(context.WithdrawalRequest.Amount, minBalance) ?? false))
                throw new BadRequestException($"Insufficient balance.");

            if (context.CentaurusContext.IsAlpha) //if it's Alpha than we need to build transaction
                context.TransactionQuantum.Transaction = context.PaymentProvider.BuildTransaction(context.WithdrawalRequest);
            else
                context.PaymentProvider.ValidateTransaction(context.TransactionQuantum.Transaction, context.WithdrawalRequest);

            return Task.CompletedTask;
        }

        public override WithdrawalProcessorContext GetContext(EffectProcessorsContainer container)
        {
            return new WithdrawalProcessorContext(container);
        }
    }
}