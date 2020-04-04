using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class ConstellationSettingsQuantum : Quantum
    {        
        [XdrField(0)]
        public RawPubKey Vault { get; set; }

        [XdrField(1)]
        public List<RawPubKey> Auditors { get; set; }

        [XdrField(2)]
        public long MinAccountBalance { get; set; }

        [XdrField(3)]
        public long MinAllowedLotSize { get; set; }

        [XdrField(4)]
        public List<AssetSettings> Assets { get; set; }

        [XdrField(5, Optional = true)]
        public RequestRateLimits RequestRateLimits { get; set; }
    }
}
