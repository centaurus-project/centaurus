using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class ProviderAssetPersistentModel
    {
        [Key(0)]
        public string CentaurusAsset { get; set; }

        [Key(1)]
        public string Token { get; set; }

        [Key(2)]
        public bool IsVirtual { get; set; }
    }
}
