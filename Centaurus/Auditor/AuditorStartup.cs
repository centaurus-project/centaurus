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

        public override Task Run(ManualResetEvent resetEvent)
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

            return Task.CompletedTask;
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

        public override async Task Shutdown()
        {
            try
            {
                await syncRoot.WaitAsync();
                isAborted = true;
                Unsubscribe(auditor);
                await CloseConnection(auditor);
                syncRoot.Dispose();
            }
            catch
            {
                syncRoot.Release();
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

        private async void OnConnectionStateChanged((BaseWebSocketConnection connection, ConnectionState prev, ConnectionState current) args)
        {
            switch (args.current)
            {
                case ConnectionState.Ready:
                    Ready(args.connection);
                    break;
                case ConnectionState.Closed:
                    await Close(args.connection);
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

        private async Task Close(BaseWebSocketConnection e)
        {
            await syncRoot.WaitAsync();
            try
            {
                Context.AppState.State = ApplicationState.Running;
                Unsubscribe(auditor);
                await CloseConnection(auditor);
                auditor = null;
                if (!isAborted)
                    InternalRun();
            }
            finally
            {
                syncRoot.Release();
            }
        }

        private readonly SemaphoreSlim syncRoot = new SemaphoreSlim(1);
    }
}
