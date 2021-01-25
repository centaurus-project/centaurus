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
    public class AuditorStartup : IStartup<AuditorSettings>
    {
        private AuditorWebSocketConnection auditor;

        private bool isAborted = false;

        private Logger logger = LogManager.GetCurrentClassLogger();
        private ManualResetEvent resetEvent;

        public async Task Run(AuditorSettings settings, ManualResetEvent resetEvent)
        {
            try
            {
                this.resetEvent = resetEvent;

                await Global.Setup(settings, new MongoStorage());

                Global.AppState.StateChanged += StateChanged;

                MessageHandlers<AuditorWebSocketConnection>.Init();

                _ = InternalRun();
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                if (Global.AppState != null)
                    Global.AppState.State = ApplicationState.Failed;
                resetEvent.Set();
            }
        }

        private async Task InternalRun()
        {
            try
            {
                while (auditor == null && !isAborted)
                {
                    var _auditor = new AuditorWebSocketConnection(new ClientWebSocket(), null);
                    try
                    {
                        Subscribe(_auditor);
                        await _auditor.EstablishConnection();
                        auditor = _auditor;
                    }
                    catch (Exception exc)
                    {
                        Unsubscribe(_auditor);
                        await CloseConnection(_auditor);

                        if (!(exc is TaskCanceledException || exc is OperationCanceledException))
                            logger.Info(exc, "Unable establish connection. Retry in 5000ms");
                        Thread.Sleep(5000);
                    }
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Auditor startup error.");
                Global.AppState.State = ApplicationState.Failed;
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

        public async Task Shutdown()
        {
            isAborted = true;
            Unsubscribe(auditor);
            await CloseConnection(auditor);
            await Global.TearDown();
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

        private void OnConnectionStateChanged(object sender, ConnectionState e)
        {
            switch (e)
            {
                case ConnectionState.Ready:
                    Ready((BaseWebSocketConnection)sender);
                    break;
                case ConnectionState.Closed:
                    Close((BaseWebSocketConnection)sender);
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
            if (Global.AppState.State != ApplicationState.WaitingForInit)
                Global.AppState.State = ApplicationState.Ready;
        }

        private async void Close(BaseWebSocketConnection e)
        {
            Global.AppState.State = ApplicationState.Running;
            Unsubscribe(auditor);
            await CloseConnection(auditor);
            auditor = null;
            if (!isAborted)
                _ = InternalRun();
        }
    }
}
