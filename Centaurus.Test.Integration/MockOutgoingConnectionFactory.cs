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
        private readonly Dictionary<string, StartupWrapper> startups;

        public MockOutgoingConnectionFactory(Dictionary<string, StartupWrapper> startups)
        {
            this.startups = startups ?? throw new ArgumentNullException(nameof(startups));
        }

        public override OutgoingConnectionWrapperBase GetConnection()
        {
            return new MockConnectionWrapper(startups);
        }
    }
}
