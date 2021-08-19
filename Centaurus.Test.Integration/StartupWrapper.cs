using Centaurus.Alpha;
using Centaurus.Controllers;
using Centaurus.PersistentStorage.Abstraction;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class StartupWrapper : IDisposable
    {
        public StartupWrapper(Settings settings, ManualResetEvent resetEvent)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Storage = GetStorage() ?? throw new ArgumentNullException(nameof(Storage));
            this.resetEvent = resetEvent ?? throw new ArgumentNullException(nameof(resetEvent));
        }

        public MockPaymentProviderFactory ProviderFactory { get; } = new MockPaymentProviderFactory();

        public Settings Settings { get; }

        public ConstellationController ConstellationController { get; private set; }

        public Startup Startup { get; private set; }

        public IPersistentStorage Storage { get; }

        public Domain.ExecutionContext Context => Startup?.Context;

        public void Run(Dictionary<string, StartupWrapper> startups)
        {
            if (Startup != null)
                throw new InvalidOperationException("Already running.");

            var context = new Domain.ExecutionContext(Settings, Storage, ProviderFactory, new MockOutgoingConnectionFactory(startups));

            ConstellationController = new ConstellationController(context);
            Startup = new Startup(context, new MockHostFactory());
            Startup.Run(resetEvent);
        }

        public void Shutdown()
        {
            if (Startup == null)
                throw new InvalidOperationException("Not running yet.");
            Startup.Shutdown();
            Startup = null;
        }

        protected ManualResetEvent resetEvent;

        protected virtual IPersistentStorage GetStorage()
        {
            return new MockStorage();
        }

        public void Dispose()
        {
            var db = Startup.Context.PermanentStorage;
            Shutdown();
        }
    }

    public class MockHostFactory : AlphaHostFactoryBase
    {
        public override IHost GetHost(Domain.ExecutionContext context)
        {
            return null;
        }
    }
}
