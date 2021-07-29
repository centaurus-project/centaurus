using Centaurus.Domain;
using Centaurus.Models;
using Centaurus.PaymentProvider;
using Centaurus.PaymentProvider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test
{
    public class MockPaymentProviderFactory : PaymentProvidersFactoryBase
    {
        public override PaymentProviderBase GetProvider(SettingsModel providerSettings, string path, string config)
        {
            return new MockPaymentProvider(providerSettings, config);
        }
    }
}
