using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Centaurus.Client
{
    public abstract class OutgoingConnectionFactoryBase
    {
        public abstract OutgoingConnectionWrapperBase GetConnection();

        public static OutgoingConnectionFactoryBase Default { get; } = new OutgoingConnectionFactory();
    }

    public class OutgoingConnectionFactory: OutgoingConnectionFactoryBase
    {
        public override OutgoingConnectionWrapperBase GetConnection()
        {
            return new ClientConnectionWrapper(new ClientWebSocket());
        }
    }
}