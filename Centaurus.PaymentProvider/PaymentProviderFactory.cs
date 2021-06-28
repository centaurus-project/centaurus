using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus.PaymentProvider
{
    public abstract class PaymentProviderFactoryBase
    {
        public abstract PaymentProviderBase GetProvider(PaymentParserBase parser, ProviderSettings providerSettings, dynamic config, WithdrawalStorage storage);

        public abstract PaymentParserBase GetParser(string name);

        public static PaymentProviderFactoryBase Default { get; } = new PaymentProviderFactoryDefault();
    }

    public class PaymentProviderFactoryDefault : PaymentProviderFactoryBase
    {
        public override PaymentParserBase GetParser(string name)
        {
            var parserType = ProviderDiscoverer.DiscoverParser(name);
            return (PaymentParserBase)Activator.CreateInstance(parserType, new[] { name });
        }

        public override PaymentProviderBase GetProvider(PaymentParserBase parser, ProviderSettings providerSettings, dynamic config, WithdrawalStorage storage)
        {
            if (!providerTypes.TryGetValue(providerSettings.Provider, out var providerType))
                providerType = ProviderDiscoverer.DiscoverProvider(providerSettings.Provider);

            return (PaymentProviderBase)Activator.CreateInstance(providerType, new[] { parser, providerSettings, config, storage });
        }

        private static Dictionary<string, Type> providerTypes = new Dictionary<string, Type>();
    }
}