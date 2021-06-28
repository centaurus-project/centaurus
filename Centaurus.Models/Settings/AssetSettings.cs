using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Asset settings and cluster-specific limits.
    /// </summary>
    [XdrContract]
    public class AssetSettings
    {
        /// <summary>
        /// Unique asset id.
        /// </summary>
        [XdrField(0)]
        public int Id { get; set; }

        /// <summary>
        /// Asset code.
        /// </summary>
        [XdrField(1)]
        public string Code { get; set; }

        /// <summary>
        /// Is native asset or not.
        /// </summary>
        [XdrField(2)]
        public long IsSuspended { get; set; }
    }
}