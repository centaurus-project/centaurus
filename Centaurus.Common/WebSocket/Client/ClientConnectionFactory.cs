using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Centaurus.Client
{
    public abstract class ClientConnectionFactoryBase
    {
        public abstract ClientConnectionWrapperBase GetConnection();

        public static ClientConnectionFactoryBase Default { get; } = new ClientConnectionFactory();
    }

    public class ClientConnectionFactory: ClientConnectionFactoryBase
    {
        public override ClientConnectionWrapperBase GetConnection()
        {
            return new ClientConnectionWrapper(new ClientWebSocket());
        }
    }
}
