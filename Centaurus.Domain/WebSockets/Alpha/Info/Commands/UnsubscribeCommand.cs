using Centaurus.Models;
using System.Collections.Generic;

namespace Centaurus.Domain
{
    [Command("Unsubscribe")]
    public class UnsubscribeCommand: BaseCommand
    {
        public List<string> Subscriptions { get; set; }
    }
}