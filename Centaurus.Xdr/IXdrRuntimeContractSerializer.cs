using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public interface IXdrRuntimeContractSerializer
    {
        public void Deserialize(object value, XdrReader reader);

        public void Serialize(object value, XdrWriter writer);
    }
}
