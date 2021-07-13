using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Stellar.Models
{
    public class AccountModel
    {
        public stellar_dotnet_sdk.KeyPair KeyPair { get; set; }
        public long SequenceNumber { get; set; }
        public List<string> ExistingTrustLines { get; set; } 
        public List<Signer> Signers { get; set; }
        public ThresholdsSettings Thresholds { get; set; }

        public class Signer
        {
            public byte[] PubKey { get; set; }

            public int Weight { get; set; }
        }

        public class ThresholdsSettings
        {
            public int Low { get; set; }

            public int Medium { get; set; }

            public int High { get; set; }
        }
    }
}
