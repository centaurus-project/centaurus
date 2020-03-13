using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Xdr
{
    /// <summary>
    /// Marks a field as a part of XDR serialization contract.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class XdrFieldAttribute: Attribute
    {
        /// <summary>
        /// Marks a field as part of XDR serialization contract.
        /// </summary>
        /// <param name="order">Serialization order (starts from 0).</param>
        /// <param name="optional">Whether a field is optional (nullable). Set to <see langword="false"/> by default.</param>
        public XdrFieldAttribute(int order)
        {
            Order = order;
        }

        /// <summary>
        /// Serialization order (starts from 0).
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Marks a field as optional (nullable).
        /// </summary>
        public bool Optional { get; set; }
    }
}
