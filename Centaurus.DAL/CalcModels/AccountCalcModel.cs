using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL
{
    public class AccountCalcModel: BaseCalcModel
    {
        public byte[] PubKey { get; set; }

        public ulong Nonce { get; set; }
    }
}
