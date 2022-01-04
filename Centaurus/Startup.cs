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

            if (context.Settings.IsPrimeNode())
                AlphaStartup = new AlphaStartup(context, alphaHostFactory);
        }

        public AlphaStartup AlphaStartup { get; }

        public void Run(ManualResetEvent resetEvent)
        {
            this.resetEvent = resetEvent ?? throw new ArgumentNullException(nameof(resetEvent));

            if (AlphaStartup != null)
                AlphaStartup.Run();

            Context.OnComplete += Context_OnComplete;
        }

        private void Context_OnComplete()
        {
            isContextStopped = true;
            var isSet = resetEvent.WaitOne(0);
            if (!isSet)
                resetEvent.Set();
        }

        bool isContextStopped;

        public void Shutdown()
        {
            if (!isContextStopped)
                Context.Complete();
            if (AlphaStartup != null)
                AlphaStartup.Shutdown();
        }
    }
}