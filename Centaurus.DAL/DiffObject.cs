using Centaurus.PersistentStorage;
using System.Collections.Generic;

namespace Centaurus.DAL
{
    public class DiffObject
    {
        public List<IPersistentModel> Batch { get; } = new List<IPersistentModel>();

        public HashSet<string> Cursors { get; } = new HashSet<string>();

        public HashSet<ulong> Accounts { get; } = new HashSet<ulong>();

        public int QuantaCount { get; set; }

        public int EffectsCount { get; set; }
    }
}