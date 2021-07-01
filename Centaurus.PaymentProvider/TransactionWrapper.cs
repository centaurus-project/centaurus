using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PaymentProvider
{
    public class TransactionWrapper
    {
        public byte[] Transaction { get; set; }

        public byte[] Hash { get; set; }

        public List<TxSignature> Signatures { get; set; }
    }
}