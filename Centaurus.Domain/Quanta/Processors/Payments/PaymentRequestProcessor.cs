using Centaurus.Domain.Models;
using Centaurus.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class PaymentRequestProcessor : QuantumProcessorBase
    {
        public PaymentRequestProcessor(ExecutionContext context)
            :base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(PaymentRequest).Name;

        public override Task<QuantumResultMessageBase> Process(QuantumProcessingItem quantumProcessingItem)
        {
            UpdateNonce(quantumProcessingItem);

            var request = (ClientRequestQuantumBase)quantumProcessingItem.Quantum;
            var payment = (PaymentRequest)request.RequestMessage;

            var destinationAccount = Context.AccountStorage.GetAccount(payment.Destination);
            if (destinationAccount == null)
            {
                quantumProcessingItem.AddAccountCreate(Context.AccountStorage, payment.Destination, Context.ConstellationSettingsManager.Current.RequestRateLimits);
                destinationAccount = Context.AccountStorage.GetAccount(payment.Destination);
            }

            if (!destinationAccount.HasBalance(payment.Asset))
                quantumProcessingItem.AddBalanceCreate(destinationAccount, payment.Asset);
            quantumProcessingItem.AddBalanceUpdate(destinationAccount, payment.Asset, payment.Amount, UpdateSign.Plus);

            quantumProcessingItem.AddBalanceUpdate(quantumProcessingItem.Initiator, payment.Asset, payment.Amount, UpdateSign.Minus);

            var result = quantumProcessingItem.Quantum.CreateEnvelope<MessageEnvelopeSignless>().CreateResult(ResultStatusCode.Success);

            return Task.FromResult((QuantumResultMessageBase)result);
        }

        public override Task Validate(QuantumProcessingItem quantumProcessingItem)
        {
            ValidateNonce(quantumProcessingItem);

            var request = (ClientRequestQuantumBase)quantumProcessingItem.Quantum;
            var payment = (PaymentRequest)request.RequestMessage;

            if (payment.Destination == null || payment.Destination.IsZero())
                throw new BadRequestException("Destination should be valid public key");

            var baseAsset = Context.ConstellationSettingsManager.Current.QuoteAsset.Code;
            var destinationAccount = Context.AccountStorage.GetAccount(payment.Destination);
            //TODO: should we allow payment that is less than min account balance?
            if (destinationAccount == null)
            {
                if (payment.Asset != baseAsset)
                    throw new BadRequestException("Account excepts only XLM asset.");
                if (payment.Amount < Context.ConstellationSettingsManager.Current.MinAccountBalance)
                    throw new BadRequestException($"Min payment amount is {Context.ConstellationSettingsManager.Current.MinAccountBalance} for this account.");
            }

            if (payment.Destination.Equals(quantumProcessingItem.Initiator.Pubkey))
                throw new BadRequestException("Source and destination must be different public keys");

            if (payment.Amount <= 0)
                throw new BadRequestException("Amount should be greater than 0");

            var paymentAsset = Context.ConstellationSettingsManager.Current.Assets.FirstOrDefault(a => a.Code == payment.Asset);
            if (paymentAsset == null || paymentAsset.IsSuspended)
                throw new BadRequestException($"Asset {payment.Asset} is not supported or suspended.");

            var minBalance = payment.Asset == baseAsset ? Context.ConstellationSettingsManager.Current.MinAccountBalance : 0;
            if (!(quantumProcessingItem.Initiator.GetBalance(payment.Asset)?.HasSufficientBalance(payment.Amount, minBalance) ?? false))
                throw new BadRequestException("Insufficient funds");

            return Task.CompletedTask;
        }
    }
}
