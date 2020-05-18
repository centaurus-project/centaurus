using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class ExtensionConfigItem
    {
        public bool IsDisabled { get; set; }

        public string Name { get; set; }

        public Dictionary<string, string> ExtensionConfig { get; set; }
    }
}
