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
        static MethodInfo applyUpdatesMethod;

        static SnapshotHelper()
        {
            var globalType = typeof(Global);
            applyUpdatesMethod = globalType.GetMethod("ApplyUpdates", BindingFlags.NonPublic | BindingFlags.Static);
        }

        public static async Task ApplyUpdates()
        {
            await (Task)applyUpdatesMethod.Invoke(null, null);
        }
    }
}
