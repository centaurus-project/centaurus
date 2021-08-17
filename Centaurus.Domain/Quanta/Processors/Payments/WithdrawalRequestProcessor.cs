using Centaurus.Domain.Models;
using Centaurus.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{

    public class WithdrawalProcessor : QuantumProcessorBase<WithdrawalProcessorContext>
    {
        public WithdrawalProcessor(ExecutionContext context)
            : base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(WithdrawalRequest).Name;

        public override Task<QuantumResultMessageBase> Process(WithdrawalProcessorContext context)
        {
            context.UpdateNonce();

            context.AddBalanceUpdate(context.InitiatorAccount, context.WithdrawalRequest.Asset, context.WithdrawalRequest.Amount, UpdateSign.Minus);

            return Task.FromResult((QuantumResultMessageBase)context.Quantum.CreateEnvelope().CreateResult(ResultStatusCodes.Success));
        }

        public override Task Validate(WithdrawalProcessorContext context)
        {
            context.ValidateNonce();

            var sourceAccount = context.InitiatorAccount.Account;

            var centaurusAsset = context.CentaurusContext.Constellation.Assets.FirstOrDefault(a => a.Code == context.WithdrawalRequest.Asset);
            if (centaurusAsset == null || centaurusAsset.IsSuspended)
                throw new BadRequestException($"Constellation doesn't support asset '{context.WithdrawalRequest.Asset}'.");

            var providerAsset = context.PaymentProvider.Settings.Assets.FirstOrDefault(a => a.CentaurusAsset == context.WithdrawalRequest.Asset);
            if (providerAsset == null)
                throw new BadRequestException($"Current provider doesn't support withdrawal of asset {centaurusAsset.Code}.");

            var baseAsset = context.CentaurusContext.Constellation.GetBaseAsset();

            var minBalance = centaurusAsset.Code == baseAsset ? context.CentaurusContext.Constellation.MinAccountBalance : 0;
            if (!(sourceAccount.GetBalance(centaurusAsset.Code)?.HasSufficientBalance(context.WithdrawalRequest.Amount, minBalance) ?? false))
                throw new BadRequestException($"Insufficient balance.");

            var withdrawal = context.WithdrawalRequest.ToProviderModel();
            if (context.CentaurusContext.IsAlpha) //if it's Alpha than we need to build transaction
            {
                context.WithdrawalRequestQuantum.Transaction = context.PaymentProvider.BuildTransaction(withdrawal);
                context.WithdrawalRequestQuantum.ProviderId = context.PaymentProvider.Id;
            }
            //should we validate signatures here?
            else if (!context.PaymentProvider.IsTransactionValid(context.WithdrawalRequestQuantum.Transaction, withdrawal, out var error))
                throw new BadRequestException($"Transaction is invalid.\nReason: {error}");

            return Task.CompletedTask;
        }

        public override ProcessorContext GetContext(Quantum quantum, AccountWrapper account)
        {
            return new WithdrawalProcessorContext(Context, quantum, account);
        }
    }
}