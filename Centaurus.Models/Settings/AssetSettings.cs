using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Asset settings.
    /// </summary>
    [XdrContract]
    public class AssetSettings
    {
        /// <summary>
        /// Asset code.
        /// </summary>
        [XdrField(0)]
        public string Code { get; set; }

        /// <summary>
        /// Is native asset or not.
        /// </summary>
        [XdrField(1)]
        public bool IsSuspended { get; set; }
    }
}