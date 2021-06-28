using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Stellar.Models
{
    public class TxBuilderAccountModel : ITransactionBuilderAccount
    {
        public long SequenceNumber { get; set; }

        public KeyPair KeyPair { get; set; }

        public string AccountId => KeyPair.AccountId;

        public IAccountId MuxedAccount => KeyPair;

        public long IncrementedSequenceNumber => SequenceNumber + 1;

        public void IncrementSequenceNumber()
        {
            SequenceNumber++;
        }
    }
}
