using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    /// <summary>
    /// Asset settings and cluster-specific limits.
    /// </summary>
    public class AssetSettings: IXdrSerializableModel
    {
        /// <summary>
        /// Unique asset id.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Asset code.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Asset issuer address.
        /// </summary>
        public RawPubKey Issuer { get; set; }

        public bool IsXlm { get { return Issuer == null; } }

        public override string ToString()
        {
            if (IsXlm) return "XLM";
            return $"{Code}-{Issuer}";
        }
    }
}
