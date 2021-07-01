using Centaurus.PaymentProvider;

namespace Centaurus.Domain
{
    public interface ITransactionProcessorContext
    {
        PaymentProviderBase PaymentProvider { get; }

        byte[] Transaction { get; }
    }
}
