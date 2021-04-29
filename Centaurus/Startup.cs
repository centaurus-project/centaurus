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
    public abstract class StartupBase
    {
        public StartupBase(Domain.ExecutionContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public abstract Task Run(ManualResetEvent resetEvent);
        public abstract Task Shutdown();
        public Domain.ExecutionContext Context { get; }

        public static StartupBase GetStartup(BaseSettings settings)
        {
            var startup = default(StartupBase);
            var storage = new MongoStorage();
            var stellarDataProvider = new StellarDataProvider(settings.NetworkPassphrase, settings.HorizonUrl);
            if (settings is AlphaSettings alphaSettings)
                startup = new AlphaStartup(new AlphaContext(alphaSettings, storage, stellarDataProvider));
            else if (settings is AuditorSettings auditorSettings)
                startup = new AuditorStartup(new AuditorContext(auditorSettings, storage, stellarDataProvider), () => new ClientConnectionWrapper(new ClientWebSocket()));
            else
                throw new NotSupportedException("Unknown settings type.");
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