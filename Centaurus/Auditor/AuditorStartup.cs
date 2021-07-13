using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Client
{
    public class AuditorStartup : StartupBase
    {
        private OutgoingWebSocketConnection auditor;

        private bool isAborted = false;

        private Logger logger = LogManager.GetCurrentClassLogger();
        private ManualResetEvent resetEvent;

        private ClientConnectionFactoryBase connectionFactory;

        public AuditorStartup(Domain.ExecutionContext context, ClientConnectionFactoryBase connectionFactory)
            : base(context)
        {
            this.connectionFactory = connectionFactory;
        }

        public override void Run(ManualResetEvent resetEvent)
        {
            try
            {
                this.resetEvent = resetEvent;
                Context.AppState.StateChanged += StateChanged;

                InternalRun();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                if (Context.AppState != null)
                    Context.AppState.State = ApplicationState.Failed;
                var isSet = resetEvent.WaitOne(0);
                if (!isSet)
                    resetEvent.Set();
            }
        }

        private void InternalRun()
        {
            var runTask = Task.Factory.StartNew(async () =>
            {
                try
                {
                    while (!isAborted)
                    {
                        var _auditor = new OutgoingWebSocketConnection(Context, connectionFactory.GetConnection());
                        try
                        {
                            Subscribe(_auditor);
                            await _auditor.EstablishConnection();
                            auditor = _auditor;
                            break;
                        }
                        catch (Exception exc)
                        {
                            Unsubscribe(_auditor);
                            await CloseConnection(_auditor);

                            if (!(exc is OperationCanceledException))
                                logger.Info(exc, "Unable establish connection. Retry in 5000ms");
                            Thread.Sleep(5000);
                        }
                    }
                }
                catch (Exception exc)
                {
                    logger.Error(exc, "Auditor startup error.");
                    Context.AppState.State = ApplicationState.Failed;
                }
            }).Unwrap();
        }

        private void StateChanged(StateChangedEventArgs eventArgs)
        {
            if (eventArgs.State == ApplicationState.Failed)
            {
                Console.WriteLine("Application failed.");
                resetEvent.Set();
            }
        }

        public override void Shutdown()
        {
            lock(syncRoot)
            {
                isAborted = true;
                Unsubscribe(auditor);
                CloseConnection(auditor).Wait();
            }
        }

        private void Subscribe(OutgoingWebSocketConnection _auditor)
        {
            if (_auditor != null)
            {
                _auditor.OnConnectionStateChanged += OnConnectionStateChanged;
            }
        }

        private void Unsubscribe(OutgoingWebSocketConnection _auditor)
        {
            if (_auditor != null)
            {
                _auditor.OnConnectionStateChanged -= OnConnectionStateChanged;
            }
        }

        private void OnConnectionStateChanged((BaseWebSocketConnection connection, ConnectionState prev, ConnectionState current) args)
        {
            switch (args.current)
            {
                case ConnectionState.Ready:
                    Ready(args.connection);
                    break;
                case ConnectionState.Closed:
                    Close(args.connection);
                    break;
                default:
                    break;
            }
        }

        private async Task CloseConnection(OutgoingWebSocketConnection _auditor)
        {
            if (_auditor != null)
            {
                await _auditor.CloseConnection();
                _auditor.Dispose();
            }
        }

        private void Ready(BaseWebSocketConnection e)
        {
            if (Context.AppState.State != ApplicationState.WaitingForInit)
                Context.AppState.State = ApplicationState.Ready;
        }

        private void Close(BaseWebSocketConnection e)
        {
            lock(syncRoot)
            {
                Context.AppState.State = ApplicationState.Running;
                Unsubscribe(auditor);
                CloseConnection(auditor).Wait();
                auditor = null;
                if (!isAborted)
                    InternalRun();
            }
        }

        private readonly object syncRoot = new { };
    }
}
