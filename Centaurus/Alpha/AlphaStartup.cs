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
    public class AlphaStartup : StartupBase
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        private IHost host;
        private ManualResetEvent resetEvent;

        public AlphaStartup(Domain.ExecutionContext context, AlphaHostFactoryBase hostFactory)
            : base(context)
        {
            host = hostFactory.GetHost(context);
        }

        public override void Run(ManualResetEvent resetEvent)
        {
            try
            {
                this.resetEvent = resetEvent;

                _ = host.RunAsync();

                ConfigureConstellation();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                if (Context.AppState != null)
                    Context.AppState.State = ApplicationState.Failed;
                throw;
            }
        }

        public override void Shutdown()
        {
            ShutdownInternal().Wait();
        }

        #region Private Members

        private async Task ShutdownInternal()
        {
            await Context.ConnectionManager.CloseAllConnections();
            await Context.InfoConnectionManager.CloseAllConnections();
            if (host != null)
            {
                await host.StopAsync(CancellationToken.None);
                host = null;
            }
        }

        private void ConfigureConstellation()
        {
            Context.AppState.StateChanged += Current_StateChanged;
        }

        private void Current_StateChanged(StateChangedEventArgs eventArgs)
        {
            if (eventArgs.State == ApplicationState.Failed)
            {
                var isSet = resetEvent.WaitOne(0);
                if (!isSet)
                    resetEvent.Set();
            }
        }

        #endregion
    }
}