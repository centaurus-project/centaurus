using Centaurus.Models;
using Centaurus.PaymentProvider;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test
{
    public class MockPaymentProviderFactory : PaymentProviderFactoryBase
    {
        public override PaymentProviderBase GetProvider(ProviderSettings providerSettings, string config)
        {
            return new MockPaymentProvider(providerSettings, config);
        }
    }
}
