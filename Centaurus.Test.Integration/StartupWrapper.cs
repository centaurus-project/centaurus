using Centaurus.Controllers;
using Centaurus.DAL;
using Centaurus.Domain;
using Centaurus.Stellar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public abstract class StartupWrapper<TStartup>: IDisposable
        where TStartup : StartupBase
    {
        public StartupWrapper(Settings settings, StellarDataProviderBase stellarDataProvider, ManualResetEvent resetEvent)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Storage = GetStorage() ?? throw new ArgumentNullException(nameof(Storage));
            this.stellarDataProvider = stellarDataProvider ?? throw new ArgumentNullException(nameof(stellarDataProvider));
            this.resetEvent = resetEvent ?? throw new ArgumentNullException(nameof(resetEvent));
        }

        public Settings Settings { get; }

        public TStartup Startup { get; private set; }

        public IStorage Storage { get; }

        public Domain.ExecutionContext Context => Startup?.Context;

        public virtual async Task Run()
        {
            if (Startup != null)
                throw new InvalidOperationException("Already running.");
            Startup = GenarateStartup();
            await Startup.Run(resetEvent);
        }

        public virtual async Task Shutdown()
        {
            if (Startup == null)
                throw new InvalidOperationException("Not running yet.");
            await Startup.Shutdown();
            Startup = null;
        }

        public abstract TStartup GenarateStartup();


        protected StellarDataProviderBase stellarDataProvider;

        protected ManualResetEvent resetEvent;

        protected virtual IStorage GetStorage()
        {
            return new MockStorage();
        }

        public void Dispose()
        {
            var db = Startup.Context.PermanentStorage;
            Shutdown().Wait();
            db.DropDatabase().Wait();
        }
    }

    public class AlphaStartupWrapper : StartupWrapper<AlphaStartup>
    {
        public AlphaStartupWrapper(Settings settings, StellarDataProviderBase stellarDataProvider, ManualResetEvent resetEvent)
            : base(settings, stellarDataProvider, resetEvent)
        {

        }

        public ConstellationController ConstellationController { get; private set; }

        public override AlphaStartup GenarateStartup()
        {
            var ExecutionContext = new Domain.ExecutionContext(Settings, Storage, stellarDataProvider);
            var alphaStartup = new AlphaStartup(ExecutionContext, null);
            return alphaStartup;
        }

        public override async Task Run()
        {
            await base.Run();
            ConstellationController = new ConstellationController(Context);
        }

        public override async Task Shutdown()
        {
            await base.Shutdown();
            ConstellationController = null;
        }
    }

    public class AuditorStartupWrapper : StartupWrapper<AuditorStartup>
    {
        private Func<ClientConnectionWrapperBase> connectionFactory;

        public AuditorStartupWrapper(Settings settings, StellarDataProviderBase stellarDataProvider, ManualResetEvent resetEvent, Func<ClientConnectionWrapperBase> connectionFactory)
            : base(settings, stellarDataProvider, resetEvent)
        {
            this.connectionFactory = connectionFactory;
        }

        public override AuditorStartup GenarateStartup()
        {
            var context = new Domain.ExecutionContext(Settings, Storage, stellarDataProvider);
            var startup = new AuditorStartup(context, connectionFactory);
            return startup;
        }
    }
}
