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
        static MethodInfo refreshUpdatesContainerMethod;

        static SnapshotHelper()
        {
            var updatesManagerType = typeof(PendingUpdatesManager);
            applyUpdatesMethod = updatesManagerType.GetMethod("ApplyUpdates", BindingFlags.NonPublic | BindingFlags.Instance);
            refreshUpdatesContainerMethod = updatesManagerType.GetMethod("RefreshUpdatesContainer", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static async Task ApplyUpdates(CentaurusContext context)
        {
            await (Task)applyUpdatesMethod.Invoke(context.PendingUpdatesManager, new object[] { context.PendingUpdatesManager.Current });
            refreshUpdatesContainerMethod.Invoke(context.PendingUpdatesManager, null);
        }
    }
}
