using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Xdr
{
    public interface IXdrRuntimeContractSerializer
    {
        public void Deserialize(object value, XdrReader reader);

        public void Serialize(object value, XdrWriter writer);
    }
}
