using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    [Command("Subscribe")]
    public class SubscribeCommand: BaseCommand
    {
        public List<string> Subscriptions { get; set; }
    }
}
