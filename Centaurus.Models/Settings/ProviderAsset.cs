using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class ProviderAsset
    {
        /// <summary>
        /// Token code. If null then it's native asset.
        /// </summary>
        [XdrField(0, Optional = true)]
        public string Token { get; set; }

        /// <summary>
        /// Asset to map to.
        /// </summary>
        [XdrField(1)]
        public string CentaurusAsset { get; set; }

        /// <summary>
        /// Is native asset or not.
        /// </summary>
        [XdrField(2)]
        public bool IsVirtual { get; set; }
    }
}
