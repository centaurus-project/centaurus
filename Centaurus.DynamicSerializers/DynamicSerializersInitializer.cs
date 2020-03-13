using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public static class DynamicSerializersInitializer
    {
        public static void Init()
        {
            var allTypes = typeof(DynamicSerializersInitializer).Assembly.GetTypes();
            foreach (var type in allTypes)
            {
                XdrConverter.RegisterSerializer(type);
            }            
        }
    }
}
