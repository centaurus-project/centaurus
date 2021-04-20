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
            : base(context)
        {
        }

        public override async Task Run(ManualResetEvent resetEvent)
        {
            try
            {
                this.resetEvent = resetEvent;

                //TODO: mock host
                if (!EnvironmentHelper.IsTest)
                {
                    host = new AlphaHostBuilder(Context).CreateHost(Context.Settings);
                    _ = host.RunAsync();
                }

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
            if (host != null)
            {
                await Context.ConnectionManager.CloseAllConnections();
                await Context.InfoConnectionManager.CloseAllConnections();
                Context.Dispose();
                if (host != null)
                {
                    await host.StopAsync(CancellationToken.None);
                    host = null;
                }
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