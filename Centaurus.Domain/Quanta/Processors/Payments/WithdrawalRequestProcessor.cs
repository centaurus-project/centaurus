using Centaurus.Domain.Models;
using Centaurus.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Domain
{

    public class WithdrawalProcessor : QuantumProcessorBase
    {
        public WithdrawalProcessor(ExecutionContext context)
            : base(context)
        {

        }

        public override string SupportedMessageType { get; } = typeof(WithdrawalRequest).Name;

        public override Task<QuantumResultMessageBase> Process(QuantumProcessingItem quantumProcessingItem)
        {
            UpdateNonce(quantumProcessingItem);

            var withdrawalQuantum = (WithdrawalRequestQuantum)quantumProcessingItem.Quantum;
            var withdrawalRequest = (WithdrawalRequest)withdrawalQuantum.RequestMessage;

            quantumProcessingItem.AddBalanceUpdate(quantumProcessingItem.Initiator, withdrawalRequest.Asset, withdrawalRequest.Amount + withdrawalRequest.Fee, UpdateSign.Minus);

            return Task.FromResult((QuantumResultMessageBase)withdrawalQuantum.CreateEnvelope<MessageEnvelopeSignless>().CreateResult(ResultStatusCode.Success));
        }

        public override Task Validate(QuantumProcessingItem quantumProcessingItem)
        {
            ValidateNonce(quantumProcessingItem);

            var sourceAccount = quantumProcessingItem.Initiator;
            var withdrawalQuantum = (WithdrawalRequestQuantum)quantumProcessingItem.Quantum;
            var withdrawalRequest = (WithdrawalRequest)withdrawalQuantum.RequestMessage;

            var centaurusAsset = Context.ConstellationSettingsManager.Current.Assets.FirstOrDefault(a => a.Code == withdrawalRequest.Asset);
            if (centaurusAsset == null || centaurusAsset.IsSuspended)
                throw new BadRequestException($"Constellation doesn't support asset '{withdrawalRequest.Asset}'.");

            if (!Context.PaymentProvidersManager.TryGetManager(withdrawalRequest.Provider, out var paymentProvider))
                throw new BadRequestException($"{withdrawalRequest.Asset} provider is not supported.");

            var providerAsset = paymentProvider.Settings.Assets.FirstOrDefault(a => a.CentaurusAsset == withdrawalRequest.Asset);
            if (providerAsset == null)
                throw new BadRequestException($"Current provider doesn't support withdrawal of asset {centaurusAsset.Code}.");

            var baseAsset = Context.ConstellationSettingsManager.Current.QuoteAsset.Code;

            var minBalance = centaurusAsset.Code == baseAsset ? Context.ConstellationSettingsManager.Current.MinAccountBalance : 0;
            if (!(sourceAccount.GetBalance(centaurusAsset.Code)?.HasSufficientBalance(withdrawalRequest.Amount + withdrawalRequest.Fee, minBalance) ?? false))
                throw new BadRequestException($"Insufficient balance.");

            var withdrawal = withdrawalRequest.ToProviderModel();
            if (withdrawalQuantum.Transaction == null) //build transaction if it wasn't build yet
            {
                withdrawalQuantum.Transaction = paymentProvider.BuildTransaction(withdrawal);
                withdrawalQuantum.Provider = paymentProvider.Id;
            }

            //should we validate signatures here?
            else if (!paymentProvider.IsTransactionValid(withdrawalQuantum.Transaction, withdrawal, out var error))
                throw new BadRequestException($"Transaction is invalid.\nReason: {error}");

            return Task.CompletedTask;
        }
    }
}