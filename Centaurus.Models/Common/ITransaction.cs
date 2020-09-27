using stellar_dotnet_sdk.xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public interface ITransaction
    {
        public byte[] TransactionXdr { get; set; }
    }
}