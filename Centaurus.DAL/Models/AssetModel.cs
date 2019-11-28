using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class AssetModel
    {
        public ulong Apex { get; set; }

        public int AssetId { get; set; }

        public string Code { get; set; }

        public byte[] Issuer { get; set; }
    }
}
