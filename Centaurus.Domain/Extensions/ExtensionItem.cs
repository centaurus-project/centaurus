using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Centaurus.Domain
{
    public class ExtensionItem
    {
        private ExtensionItem(string name, Version version, Dictionary<string, string> config, IExtension extensionInstance)
        {
            Name = name;
            Version = version;
            ExtensionInstance = extensionInstance;
            Config = config;
        }

        public string Name { get; }

        public Version Version { get; }

        public IExtension ExtensionInstance { get; }

        public Dictionary<string, string> Config { get; }

        public static ExtensionItem Load(ExtensionConfigItem extensionConfigItem, string extensionsDirectory)
        {
            var extensionPath = Path.Combine(extensionsDirectory, extensionConfigItem.Name + ".dll");
            if (!File.Exists(extensionPath))
                throw new Exception($"Extension {extensionConfigItem.Name} is not found.");
            var extensionAssenmbly = Assembly.LoadFile(extensionPath);
            var extensionTypes = extensionAssenmbly.GetTypes().Where(t => typeof(IExtension).IsAssignableFrom(t));
            if (extensionTypes.Count() < 1)
                throw new Exception($"Extension {extensionConfigItem.Name} doesn't contain types that implement IExtension interface.");
            else if (extensionTypes.Count() > 1)
                throw new Exception($"Extension {extensionConfigItem.Name} contains multiple types that implement IExtension interface.");

            return new ExtensionItem(extensionConfigItem.Name, extensionAssenmbly.GetName().Version, extensionConfigItem.ExtensionConfig, (IExtension)Activator.CreateInstance(extensionTypes.First()));
        }
    }
}
