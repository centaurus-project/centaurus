using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class CommandWrapper
    {
        public string Command { get; set; }

        public BaseCommand CommandObject { get; set; }
    }
}
