using Centaurus.PaymentProvider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Centaurus.Domain
{
    public class PaymentProvidersDiscoverer
    {
        public static Type DiscoverProvider(string providerName, string assemblyPath)
        {
            var assembly = GetProviderAssembly(providerName, assemblyPath);
            return DiscoverType(assembly);
        }

        private static Type DiscoverType(Assembly assembly)
        {
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

        static Type baseType = typeof(PaymentProviderBase);

        private static Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>();

        private static Assembly GetProviderAssembly(string providerName, string assemblyPath)
        {
            if (assemblies.TryGetValue(providerName, out var assembly))
                return assembly;

            if (string.IsNullOrEmpty(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));

            if (!File.Exists(assemblyPath))
                throw new Exception($"{assemblyPath} is not found.");
            assembly = Assembly.LoadFrom(assemblyPath);
            assemblies.Add(providerName, assembly);
            return assembly;
        }
    }
}
