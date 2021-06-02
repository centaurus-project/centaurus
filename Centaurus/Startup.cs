using Centaurus.DAL.Mongo;
using Centaurus.Domain;
using Centaurus.Stellar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    public abstract class StartupBase: ContextualBase
    {
        public StartupBase(Domain.ExecutionContext context)
            :base(context)
        {
        }

        public abstract Task Run(ManualResetEvent resetEvent);
        public abstract Task Shutdown();
        public Domain.ExecutionContext Context { get; }

        public static StartupBase GetStartup(Settings settings)
        {
            var startup = default(StartupBase);
            var storage = new MongoStorage();
            var stellarDataProvider = new StellarDataProvider(settings.NetworkPassphrase, settings.HorizonUrl);
            var context = new Domain.ExecutionContext(settings, storage, stellarDataProvider);
            if (context.IsAlpha)
                startup = new AlphaStartup(context);
            else
                startup = new AuditorStartup(context, () => new ClientConnectionWrapper(new ClientWebSocket()));
            return startup;
        }
    }

    public abstract class StartupBase<TContext>: StartupBase
        where TContext: Domain.ExecutionContext
    {
        public StartupBase(TContext context)
            :base(context)
        {
        }

        public new TContext Context => (TContext)base.Context;
    }
}