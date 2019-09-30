using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class ISerializableModelExtensions
    {
        public static byte[] ComputeHash(this IXdrSerializableModel model)
        {
            return XdrConverter.Serialize(model).ComputeHash();
        }

        public static IXdrSerializableModel Clone(this IXdrSerializableModel model)
        {
            return XdrConverter.Deserialize(XdrConverter.Serialize(model), model.GetType());
        }
    }
}
