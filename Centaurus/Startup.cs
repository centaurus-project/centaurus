using Centaurus.Alpha;
using Centaurus.Domain;
using Centaurus.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    public class Startup : ContextualBase
    {
        private ManualResetEvent resetEvent;

        public Startup(Domain.ExecutionContext context, AlphaHostFactoryBase alphaHostFactory)
            : base(context)
        {
            if (alphaHostFactory == null)
                throw new ArgumentNullException(nameof(alphaHostFactory));

            if (context.RoleManager.ParticipationLevel == CentaurusNodeParticipationLevel.Prime)
                AlphaStartup = new AlphaStartup(context, alphaHostFactory);
        }

        public AlphaStartup AlphaStartup { get; }

        public void Run(ManualResetEvent resetEvent)
        {
            this.resetEvent = resetEvent ?? throw new ArgumentNullException(nameof(resetEvent));

            if (AlphaStartup != null)
                AlphaStartup.Run();

            Context.StateManager.StateChanged += Current_StateChanged;
        }

        private void Current_StateChanged(StateChangedEventArgs eventArgs)
        {
            if (eventArgs.State == State.Failed)
            {
                var isSet = resetEvent.WaitOne(0);
                if (!isSet)
                    resetEvent.Set();
            }
        }

        public void Shutdown()
        {
            Context.StateManager.Stopped();
            if (AlphaStartup != null)
                AlphaStartup.Shutdown();

            //close all connections
            Task.WaitAll(
                Context.IncomingConnectionManager.CloseAllConnections(),
                Context.InfoConnectionManager.CloseAllConnections(),
                Task.Factory.StartNew(Context.OutgoingConnectionManager.CloseAllConnections)
            );
        }
    }
}