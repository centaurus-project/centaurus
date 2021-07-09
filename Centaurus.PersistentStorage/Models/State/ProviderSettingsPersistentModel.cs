using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PersistentStorage
{
    [MessagePackObject]
    public class ProviderSettingsPersistentModel
    {
        [Key(0)]
        public string Provider { get; set; }

        [Key(1)]
        public string Name { get; set; }

        [Key(2)]
        public string Vault { get; set; }

        [Key(3)]
        public string InitCursor { get; set; }

        [Key(4)]
        public List<ProviderAssetPersistentModel> Assets { get; set; }

        [Key(5)]
        public int PaymentSubmitDelay { get; set; }
    }
}
