using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class SettingsModel
    {
        public byte[] Vault { get; set; }

        public byte[][] Auditors { get; set; }

        public long MinAccountBalance { get; set; }
        
        public long MinAllowedLotSize { get; set; }

        public ulong Apex { get; set; }
    }
}
