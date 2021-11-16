using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public static class PaymentProviderHelper
    {
        public static string GetProviderId(string provider, string providerName)
        {
            if (string.IsNullOrWhiteSpace(provider))
                throw new ArgumentNullException(nameof(provider));


            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentNullException(nameof(providerName));

            return $"{provider}-{providerName}";
        }
    }
}
