using Centaurus.PaymentProvider.Models;
using System.Collections.Generic;

namespace Centaurus.PaymentProvider
{
    public class TransactionWrapper
    {
        public byte[] Transaction { get; set; }

        public byte[] Hash { get; set; }

        public List<SignatureModel> Signatures { get; set; }
    }
}