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
            var updatesManagerType = typeof(PendingUpdatesManager);
            applyUpdatesMethod = updatesManagerType.GetMethod("ApplyUpdates", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static async Task ApplyUpdates()
        {
            await (Task)applyUpdatesMethod.Invoke(Global.PendingUpdatesManager, null);
        }
    }
}
