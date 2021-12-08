using Centaurus.Models;
using System.Collections.Generic;

namespace Centaurus.Domain.Models
{
    public class Snapshot
    {
        public ulong Apex { get; set; }

        public byte[] LastHash { get; set; }

        public ConstellationSettings ConstellationSettings { get; set; }

        public List<Account> Accounts { get; set; }

        public List<OrderWrapper> Orders { get; set; }

        public Dictionary<string, string> Cursors { get; set; }
    }
}