using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Transaction must be build on Alpha and shared with the constellation. Each auditor can build different transactions.
    /// </summary>
    public class WithdrawalRequestQuantum: ClientRequestQuantumBase
    {
        [XdrField(0)]
        public byte[] Transaction { get; set; }

        [XdrField(1)]
        public string Provider { get; set; }

        public WithdrawalRequest WithdrawalRequest => (WithdrawalRequest)RequestEnvelope.Message;
    }
}