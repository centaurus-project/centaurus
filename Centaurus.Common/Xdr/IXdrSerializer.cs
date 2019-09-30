using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public interface IXdrSerializer<T> where T : class, IXdrSerializableModel
    {
        public void Deserialize(ref T value, XdrReader reader);

        public void Serialize(T value, XdrWriter writer);
    }
}
