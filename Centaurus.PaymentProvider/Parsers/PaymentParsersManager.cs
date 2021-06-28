using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Centaurus.PaymentProvider
{
    public class PaymentParsersManager
    {
        private Dictionary<string, PaymentParserBase> paymentParsers = new Dictionary<string, PaymentParserBase>();

        public bool TryGetParser(string provider, out PaymentParserBase parser)
        {
            if (paymentParsers.TryGetValue(provider, out parser))
                return true;
            try
            {
                var parserType = ProviderDiscoverer.DiscoverParser(provider);
                parser = (PaymentParserBase)Activator.CreateInstance(parserType, new[] { provider });
                paymentParsers.Add(provider, parser);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
