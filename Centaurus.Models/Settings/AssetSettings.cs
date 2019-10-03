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
