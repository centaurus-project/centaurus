using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class SettingsModel
    {
        public List<VaultModel> Vaults { get; set; }

        public byte[][] Auditors { get; set; }

        public long MinAccountBalance { get; set; }

        public long MinAllowedLotSize { get; set; }

        public long Apex { get; set; }

        public List<AssetModel> Assets { get; set; }

        public RequestRateLimitsModel RequestRateLimits { get; set; }
    }
}
