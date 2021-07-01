using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus.PaymentProvider
{
    public class ProviderDiscoverer
    {
        public static Type DiscoverProvider(string providerName)
        {
            return DiscoverType(providerName, typeof(PaymentProviderBase));
        }

        private static Type DiscoverType(string providerName, Type baseType)
        {
            var assembly = GetProviderAssembly(providerName);
            var parserTypes = assembly
                .GetTypes()
                .Where(x => baseType.IsAssignableFrom(x)
                    && !x.IsInterface
                    && !x.IsAbstract);

            if (parserTypes.Count() > 1)
                throw new ArgumentNullException($"{assembly.FullName} contains multiple types that implement {baseType.Name}.");
            else if (parserTypes.Count() < 1)
                throw new ArgumentNullException($"{assembly.FullName} doesn't contain types that implement {baseType.Name}.");

            return parserTypes.First();
        }

        private static Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>();

        private static Assembly GetProviderAssembly(string providerName)
        {
            if (assemblies.TryGetValue(providerName, out var assembly))
                return assembly;

            var providerDll = $"Centaurus.{providerName}.PaymentProvider.dll";
            if (!File.Exists(providerDll))
                throw new Exception($"{providerDll} is not found.");
            assembly = Assembly.LoadFile(providerDll);
            assemblies.Add(providerName, assembly);
            return assembly;
        }
    }
}
