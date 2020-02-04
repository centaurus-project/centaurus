using Centaurus.Test.Contracts;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test
{
    public static class TestDynamicSerializersInitializer
    {
        public static void Init()
        {
            var _ = new PrimitiveTypes(); //force loading assembly
            _.Bool = true;
            var allTypes = typeof(TestDynamicSerializersInitializer).Assembly.GetTypes();
            foreach (var type in allTypes)
            {
                XdrConverter.RegisterSerializer(type);
            }
        }
    }
}
