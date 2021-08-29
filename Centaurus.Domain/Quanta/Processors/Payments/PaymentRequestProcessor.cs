using Centaurus.Domain.Models;
using Centaurus.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class PaymentRequestProcessor : QuantumProcessorBase<PaymentProcessorContext>
    {
        public PaymentRequestProcessor(ExecutionContext context)
            :base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(PaymentRequest).Name;

        public override ProcessorContext GetContext(Quantum quantum, AccountWrapper account)
        {
            return new PaymentProcessorContext(Context, quantum, account);
        }

        public override Task<QuantumResultMessageBase> Process(PaymentProcessorContext context)
        {
            context.UpdateNonce();

            var payment = context.Payment;

            if (context.DestinationAccount == null)
            {
                var accId = context.CentaurusContext.AccountStorage.NextAccountId;
                context.AddAccountCreate(context.CentaurusContext.AccountStorage, accId, payment.Destination);
            }

            if (!context.DestinationAccount.HasBalance(payment.Asset))
                context.AddBalanceCreate(context.DestinationAccount, payment.Asset);
            context.AddBalanceUpdate(context.DestinationAccount, payment.Asset, payment.Amount, UpdateSign.Plus);

            context.AddBalanceUpdate(context.InitiatorAccount, payment.Asset, payment.Amount, UpdateSign.Minus);

            var result = context.Quantum.CreateEnvelope<MessageEnvelopeSignless>().CreateResult(ResultStatusCode.Success);

            return Task.FromResult((QuantumResultMessageBase)result);
        }

        public override Task Validate(PaymentProcessorContext context)
        {
            context.ValidateNonce();

            var payment = context.Payment;

            if (payment.Destination == null || payment.Destination.IsZero())
                throw new BadRequestException("Destination should be valid public key");

            var baseAsset = context.CentaurusContext.Constellation.QuoteAsset.Code;
            if (context.DestinationAccount == null)
            {
                if (payment.Asset != baseAsset)
                    throw new BadRequestException("Account excepts only XLM asset.");
                if (payment.Amount < context.CentaurusContext.Constellation.MinAccountBalance)
                    throw new BadRequestException($"Min payment amount is {context.CentaurusContext.Constellation.MinAccountBalance} for this account.");
            }

            if (payment.Destination.Equals(context.InitiatorAccount.Pubkey))
                throw new BadRequestException("Source and destination must be different public keys");

            if (payment.Amount <= 0)
                throw new BadRequestException("Amount should be greater than 0");

            if (!context.CentaurusContext.Constellation.Assets.Any(a => a.Code == payment.Asset))
                throw new BadRequestException($"Asset {payment.Asset} is not supported");

            var minBalance = payment.Asset == baseAsset ? context.CentaurusContext.Constellation.MinAccountBalance : 0;
            if (!(context.InitiatorAccount.GetBalance(payment.Asset)?.HasSufficientBalance(payment.Amount, minBalance) ?? false))
                throw new BadRequestException("Insufficient funds");

            return Task.CompletedTask;
        }
    }
}
