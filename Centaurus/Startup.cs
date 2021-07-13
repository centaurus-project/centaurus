using Centaurus.Alpha;
using Centaurus.Client;
using Centaurus.Domain;
using Centaurus.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    public abstract class StartupBase : ContextualBase
    {
        public StartupBase(Domain.ExecutionContext context)
            : base(context)
        {
        }

        public abstract void Run(ManualResetEvent resetEvent);
        public abstract void Shutdown();
    }

    public class StartupMain : StartupBase
    {
        public StartupMain(Domain.ExecutionContext context, ClientConnectionFactoryBase auditorConnectionFactory, AlphaHostFactoryBase alphaHostFactory)
            : base(context)
        {
            if (auditorConnectionFactory == null)
                throw new ArgumentNullException(nameof(auditorConnectionFactory));

            if (alphaHostFactory == null)
                throw new ArgumentNullException(nameof(alphaHostFactory));

            AuditorStartup = new AuditorStartup(context, auditorConnectionFactory);
            if (context.RoleManager.ParticipationLevel == CentaurusNodeParticipationLevel.Prime)
                AlphaStartup = new AlphaStartup(context, alphaHostFactory);
        }

        public AlphaStartup AlphaStartup { get; }

        public AuditorStartup AuditorStartup { get; }

        public override void Run(ManualResetEvent resetEvent)
        {
            AuditorStartup.Run(resetEvent);
            if (AlphaStartup != null)
                AlphaStartup.Run(resetEvent);
        }

        public override void Shutdown()
        {
            Context.AppState.State = ApplicationState.Stopped;
            AuditorStartup.Shutdown();
            if (AlphaStartup != null)
                AlphaStartup.Shutdown();
        }
    }
}