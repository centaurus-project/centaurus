using Centaurus.DAL.Mongo;
using Centaurus.Domain;
using Centaurus.Models;
using NLog;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus
{
    public class AuditorStartup: IStartup<AuditorSettings>
    {
        private AuditorWebSocketConnection auditor;

        private bool isAborted = false;

        private Logger logger = LogManager.GetCurrentClassLogger();

        public void Run(AuditorSettings settings)
        {
            Global.Init(settings, new MongoStorage());

            Global.AppState.StateChanged += StateChanged;

            MessageHandlers<AuditorWebSocketConnection>.Init();

            _ = InternalRun();
        }

        private async Task InternalRun()
        {
            try
            {
                while (auditor == null)
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

                        logger.Error(exc, "Unable establish connection. Retry in 5000ms");
                        Thread.Sleep(5000);
                    }
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc);
                Global.AppState.State = ApplicationState.Failed;
            }
        }

        private void StateChanged(object sender, ApplicationState state)
        {
            if (state == ApplicationState.Failed)
            {
                Shutdown();

                Thread.Sleep(10000);//sleep for 10 sec to make sure that pending updates are saved

                //TODO: restart after some timeout
            }
        }

        public void Shutdown()
        {
            isAborted = true;
            Unsubscribe(auditor);
            CloseConnection(auditor).Wait();
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
                await _auditor?.CloseConnection();
                _auditor?.Dispose();
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
            if (!isAborted)
                _ = InternalRun();
        }
    }
}
