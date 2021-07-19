using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test
{
    public class MockPaymentProviderFactory : PaymentProviderFactoryBase
    {
        public override PaymentProviderBase GetProvider(SettingsModel providerSettings, string config)
        {
            return new MockPaymentProvider(providerSettings, config);
        }
    }
}
