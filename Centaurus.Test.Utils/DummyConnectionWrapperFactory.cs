using Centaurus.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class DummyConnectionWrapperFactory : OutgoingConnectionFactoryBase
    {
        public DummyConnectionWrapperFactory()
        {

        }

        public override OutgoingConnectionWrapperBase GetConnection()
        {
            return new DummyConnectionWrapper(new DummyWebSocket());
        }
    }
}
