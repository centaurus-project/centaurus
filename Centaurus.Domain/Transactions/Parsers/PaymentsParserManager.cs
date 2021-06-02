using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus.Domain
{
    public static class PaymentsParserManager
    {
        private static ImmutableDictionary<PaymentProvider, PaymentsParserBase> paymentParsers;

        static PaymentsParserManager()
        {
            var discoveredParsers = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(x => typeof(PaymentsParserBase).IsAssignableFrom(x)
                    && !x.IsInterface
                    && !x.IsAbstract);

            var parsers = new Dictionary<PaymentProvider, PaymentsParserBase>();
            foreach (var parserType in discoveredParsers)
            {
                var instance = (PaymentsParserBase)Activator.CreateInstance(parserType);
                if (parsers.ContainsKey(instance.Provider))
                    throw new Exception($"Payments manager for provider {instance.Provider} is already registered");

                parsers.Add(instance.Provider, instance);
            }

            paymentParsers = parsers.ToImmutableDictionary();
        }

        public static bool TryGetParser(PaymentProvider provider, out PaymentsParserBase parser)
        {
            return paymentParsers.TryGetValue(provider, out parser);
        }
    }
}
