using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL
{
    public class OrderCalcModel: BaseCalcModel
    {
        public ulong OrderId { get; set; }

        public double Price { get; set; }

        public long Amount { get; set; }

        public byte[] Pubkey { get; set; }
    }
}
