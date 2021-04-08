using Centaurus.DAL.Mongo;
using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    public class AuditorStartup : StartupBase
    {
        private AuditorWebSocketConnection auditor;

        private bool isAborted = false;

        private Logger logger = LogManager.GetCurrentClassLogger();
        private ManualResetEvent resetEvent;

        public AuditorStartup(AuditorContext context)
            :base(context)
        {

        }

        public override async Task Run(ManualResetEvent resetEvent)
        {
            try
            {
                this.resetEvent = resetEvent;

                await Context.Init();
                Context.AppState.StateChanged += StateChanged;

                MessageHandlers<AuditorWebSocketConnection>.Init(Context);

                _ = InternalRun();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                if (Context.AppState != null)
                    Context.AppState.State = ApplicationState.Failed;
                resetEvent.Set();
            }
        }

        private async Task InternalRun()
        {
            try
            {
                while (!isAborted)
                {
                    var _auditor = new AuditorWebSocketConnection((AuditorContext)Context, new ClientWebSocket(), null);
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
                        _auditor.Dispose();

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
        }

        private void StateChanged(StateChangedEventArgs eventArgs)
        {
            if (eventArgs.State == ApplicationState.Failed)
            {
                Console.WriteLine("Application failed. Saving pending updates...");
                Thread.Sleep(PendingUpdatesManager.SaveInterval);
                resetEvent.Set();
            }
        }

        public override async Task Shutdown()
        {
            isAborted = true;
            Unsubscribe(auditor);
            await CloseConnection(auditor);
            await Context.TearDown();
        }

        private void Subscribe(AuditorWebSocketConnection _auditor)
        {
            if (_auditor != null)
            {
                _auditor.OnConnectionStateChanged += OnConnectionStateChanged;
            }
        }

        private void Unsubscribe(AuditorWebSocketConnection _auditor)
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

        private async Task CloseConnection(AuditorWebSocketConnection _auditor)
        {
            if (_auditor != null)
            {
                await _auditor.CloseConnection();
            }
        }

        private void Ready(BaseWebSocketConnection e)
        {
            if (Context.AppState.State != ApplicationState.WaitingForInit)
                Context.AppState.State = ApplicationState.Ready;
        }

        private async void Close(BaseWebSocketConnection e)
        {
            Context.AppState.State = ApplicationState.Running;
            Unsubscribe(auditor);
            await CloseConnection(auditor);
            auditor.Dispose();
            if (!isAborted)
                _ = InternalRun();
        }
    }
}
