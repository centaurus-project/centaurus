using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Xdr
{
    /// <summary>
    /// Marks a class as an XDR-serializable data contract.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class XdrContractAttribute : Attribute
    {

    }
}
