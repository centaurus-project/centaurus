using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class ConstellationSettings
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
    }
}
