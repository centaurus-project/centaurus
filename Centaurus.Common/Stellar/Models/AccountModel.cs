using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Stellar.Models
{
    public class AccountModel
    {
        public KeyPair KeyPair { get; set; }
        public long SequenceNumber { get; set; }
        public List<string> ExistingTrustLines { get; set; } 
    }
}
