using System;
using Centaurus.Domain;
using Centaurus.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using NLog;
using System.Threading.Tasks;
using System.Threading;

namespace Centaurus.Alpha
{
    public class AlphaStartup
    {
        private IHost host;

        public AlphaStartup(Domain.ExecutionContext context, AlphaHostFactoryBase hostFactory)
        {
            host = hostFactory.GetHost(context);
        }

        public void Run()
        {
            _ = host.RunAsync();
        }

        public void Shutdown()
        {
            ShutdownInternal().Wait();
        }

        #region Private Members

        private async Task ShutdownInternal()
        {
            if (host != null)
            {
                await host.StopAsync(CancellationToken.None);
                host.Dispose();
                host = null;
            }
        }

        #endregion
    }
}