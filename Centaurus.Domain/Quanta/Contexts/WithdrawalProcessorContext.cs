using Centaurus.Models;
using Centaurus.PaymentProvider;

namespace Centaurus.Domain
{
    public class WithdrawalProcessorContext : RequestContext, ITransactionProcessorContext
    {
        public WithdrawalProcessorContext(EffectProcessorsContainer effectProcessorsContainer)
            : base(effectProcessorsContainer)
        {
            if (!CentaurusContext.PaymentProvidersManager.TryGetManager(WithdrawalRequest.PaymentProvider, out var provider))
                throw new BadRequestException($"Provider {WithdrawalRequest.PaymentProvider} is not supported.");

            PaymentProvider = provider;
        }

        public RequestTransactionQuantum TransactionQuantum => (RequestTransactionQuantum)Request;

        public WithdrawalRequest WithdrawalRequest => (WithdrawalRequest)Request.RequestEnvelope.Message;

        public PaymentProviderBase PaymentProvider { get; }

        public byte[] Transaction => TransactionQuantum.Transaction;
    }
}