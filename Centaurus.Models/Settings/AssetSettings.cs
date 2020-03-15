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
        [XdrField(1, Optional = true)]
        public string Code { get; set; }

        /// <summary>
        /// Asset issuer address.
        /// </summary>
        [XdrField(2, Optional = true)]
        public RawPubKey Issuer { get; set; }

        public bool IsXlm => Issuer == null;

        public override string ToString()
        {
            if (IsXlm) return "XLM";
            return $"{Code}-{Issuer}";
        }

        /// <summary>
        /// Converts a string asset code into AssetSettings object
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static AssetSettings FromCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentNullException(nameof(code));

            var assetData = code.Split("-", StringSplitOptions.RemoveEmptyEntries);
            if (assetData.Length != 1 && assetData.Length != 3) //if length is 1 then it's a native asset, else it should have code, asset type and issuer
                throw new Exception("Unable to parse asset");

            return new AssetSettings { Code = assetData[0], Issuer = assetData.Length > 1 ? new RawPubKey(assetData[1]) : null };
        }
    }
}
