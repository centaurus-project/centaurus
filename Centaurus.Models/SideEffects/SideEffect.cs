using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    /// <summary>
    /// Side effects are effects that don't change constellation and are unique for each auditor. For example, transaction signatures
    /// </summary>
    [XdrContract]
    [XdrUnion((int)SideEffectTypes.TransactionSigned, typeof(TransactionSignedEffect))]
    public abstract class SideEffect
    {
        public abstract SideEffectTypes EffectType { get; }
    }
}