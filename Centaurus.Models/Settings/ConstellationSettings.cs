using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Models
{
    public class ConstellationSettings: IXdrSerializableModel
    {
        public RawPubKey Vault { get; set; }

        public List<RawPubKey> Auditors { get; set; }

        public long MinAccountBalance { get; set; }

        public long MinAllowedLotSize { get; set; }

        public List<AssetSettings> Assets { get; set; }
    }
}
