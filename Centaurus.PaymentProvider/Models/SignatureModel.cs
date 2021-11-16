using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PaymentProvider.Models
{
    public class SignatureModel
    {
        public byte[] Signer { get; set; }

        public byte[] Signature { get; set; }
    }
}
