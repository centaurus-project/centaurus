using Centaurus.Domain;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public static class ContextHelpers
    {
        static MethodInfo applyUpdatesMethod;
        static MethodInfo refreshUpdatesContainerMethod;

        static ContextHelpers()
        {
            var updatesManagerType = typeof(PendingUpdatesManager);
            applyUpdatesMethod = updatesManagerType.GetMethod("ApplyUpdates", BindingFlags.NonPublic | BindingFlags.Instance);
            refreshUpdatesContainerMethod = updatesManagerType.GetMethod("RefreshUpdatesContainer", BindingFlags.NonPublic | BindingFlags.Instance); 
        }

        public static async Task ApplyUpdates(ExecutionContext context)
        {
            await (Task)applyUpdatesMethod.Invoke(context.PendingUpdatesManager, new object[] { context.PendingUpdatesManager.Current });
            refreshUpdatesContainerMethod.Invoke(context.PendingUpdatesManager, null);
        }
    }
}
