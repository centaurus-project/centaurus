using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class ProviderSettings
    {
        [XdrField(0)]
        public string Provider { get; set; }

        [XdrField(1)]
        public string Name { get; set; }

        [XdrField(2)]
        public string Vault { get; set; }

        [XdrField(3)]
        public string InitCursor { get; set; }

        [XdrField(4)]
        public List<ProviderAsset> Assets { get; set; }

        /// <summary>
        /// Submit delay in seconds
        /// </summary>
        [XdrField(5)]
        public int PaymentSubmitDelay { get; set; }

        public string ProviderId => $"{Provider}-{Name}";
    }
}
