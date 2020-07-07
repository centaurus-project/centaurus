using Centaurus.Domain;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public static class SnapshotHelper
    {
        public static async Task ApplyUpdates()
        {
            var globalType = typeof(Global);
            var applyUpdatesMethod = globalType.GetMethod("ApplyUpdates", BindingFlags.NonPublic | BindingFlags.Static);

            await (Task)applyUpdatesMethod.Invoke(null, null);
        }
    }
}
