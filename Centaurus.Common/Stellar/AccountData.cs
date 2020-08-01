using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public class AccountData : ITransactionBuilderAccount
    {
        public AccountData(KeyPair keyPair, long sequenceNumber)
        {
            KeyPair = keyPair ?? throw new ArgumentNullException(nameof(keyPair));
            if (sequenceNumber < 1)
                throw new ArgumentNullException(nameof(sequenceNumber));
            SequenceNumber = sequenceNumber;
        }

        public string AccountId => KeyPair.AccountId;

        public KeyPair KeyPair { get; }

        public long SequenceNumber { get; private set; }

        public long IncrementedSequenceNumber => SequenceNumber + 1;

        public IAccountId MuxedAccount
        {
            get
            {
                return KeyPair;
            }
        }

        public void IncrementSequenceNumber()
        {
            SequenceNumber++;
        }

        public Account GetAccount()
        {
            return new Account(AccountId, SequenceNumber);
        }
    }
}
