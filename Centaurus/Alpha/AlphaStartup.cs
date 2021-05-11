using System;
using System.Net.WebSockets;
using Centaurus.Domain;
using Centaurus.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using System.Linq;
using System.Threading.Tasks;
using Centaurus.DAL.Mongo;
using Microsoft.Extensions.Logging;
using NLog.Web;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Net;
using System.Text.RegularExpressions;
using Centaurus.Alpha;

namespace Centaurus
{
    public class AlphaStartup : StartupBase<AlphaContext>
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        private IHost host;
        private ManualResetEvent resetEvent;

        public AlphaStartup(AlphaContext context)
            : this(context, GetHost)
        {
        }

        public AlphaStartup(AlphaContext context, Func<AlphaContext, IHost> hostFactory)
            : base(context)
        {
            host = hostFactory?.Invoke(context);
        }

        private static IHost GetHost(AlphaContext context)
        {
            return new AlphaHostBuilder(context).CreateHost(context.Settings);
        }

        public override async Task Run(ManualResetEvent resetEvent)
        {
            try
            {
                this.resetEvent = resetEvent;

                _ = host.RunAsync();

                await ConfigureConstellation();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                if (Context.AppState != null)
                    Context.AppState.State = ApplicationState.Failed;
                throw;
            }
        }

        public override async Task Shutdown()
        {
            Context.AppState.State = ApplicationState.Stopped;
            await Context.ConnectionManager.CloseAllConnections();
            await Context.InfoConnectionManager.CloseAllConnections();
            Context.Dispose();
            if (host != null)
            {
                await host.StopAsync(CancellationToken.None);
                host = null;
            }
        }

        #region Private Members

        private async Task ConfigureConstellation()
        {
            await Context.Init();

            Context.AppState.StateChanged += Current_StateChanged;
        }

        private void Current_StateChanged(StateChangedEventArgs eventArgs)
        {
            if (eventArgs.State == ApplicationState.Failed)
            {
                Thread.Sleep(PendingUpdatesManager.SaveInterval);
                var isSet = resetEvent.WaitOne(0);
                if (!isSet)
                    resetEvent.Set();
            }
        }

        #endregion
    }
}