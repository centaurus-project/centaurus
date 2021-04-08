using Centaurus.DAL.Mongo;
using Centaurus.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    public abstract class StartupBase
    {
        public StartupBase(CentaurusContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public abstract Task Run(ManualResetEvent resetEvent);
        public abstract Task Shutdown();
        public CentaurusContext Context { get; }

        public static StartupBase GetStartup(BaseSettings settings)
        {
            var startup = default(StartupBase);
            if (settings is AlphaSettings)
                startup = new AlphaStartup(new AlphaContext(settings, new MongoStorage()));
            else if (settings is AuditorSettings)
                startup = new AuditorStartup(new AuditorContext(settings, new MongoStorage()));
            else
                throw new NotSupportedException("Unknown settings type.");
            return startup;
        }
    }
}