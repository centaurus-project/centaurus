using Centaurus.Client;
using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test
{
    public class MockOutgoingConnectionFactory : OutgoingConnectionFactoryBase
    {
        public override OutgoingConnectionWrapperBase GetConnection()
        {
            return new MockAuditorConnectionInfo(new FakeWebSocket());
        }
    }
}
