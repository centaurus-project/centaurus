using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class BinaryDataSerializer<T> where T : BinaryData, new()
    {
        public void Deserialize(ref T value, XdrReader reader)
        {
            value.Data = reader.ReadVariable();
        }

        public void Serialize(T value, XdrWriter writer)
        {
            writer.Write(value.Data);
        }
    }
}
