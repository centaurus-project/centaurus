using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class InfoWebSocketConnection : IDisposable
    {
        static InfoCommandsHandlers commandHandlers = new InfoCommandsHandlers();

        static Logger logger = LogManager.GetCurrentClassLogger();

        private WebSocket webSocket;

        public string Ip { get; }
        public string ConnectionId { get; }

        public List<long> Subscriptions { get; } = new List<long>();

        public InfoWebSocketConnection(WebSocket webSocket, string connectionId, string ip)
        {
            this.webSocket = webSocket;
            Ip = ip;
            ConnectionId = connectionId;
        }

        public async Task CloseConnection(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string desc = null)
        {
            if (webSocket == null)
                return;
            try
            {
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting)
                {
                    desc = desc ?? status.ToString();
                    try
                    {
                        await webSocket.CloseAsync(status, desc, CancellationToken.None);
                    }
                    catch (WebSocketException exc)
                    {
                        //ignore client disconnection
                        if (exc.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely)
                            throw;
                    }
                }
                Dispose();
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
            finally
            {
                OnClosed?.Invoke(this, new EventArgs { });
            }
        }

        public event EventHandler OnClosed;

        public async Task Listen()
        {
            try
            {
                var reader = await webSocket.GetInputArray();
                while (true)
                {
                    BaseCommand command = null;
                    try
                    {
                        command = BaseCommand.Deserialize(reader);
                        var result = await commandHandlers.HandleCommand(this, command);
                        await SendMessage(result);
                    }
                    catch (Exception exc)
                    {
                        if (exc is ConnectionCloseException)
                            throw;
                        var statusCode = exc.GetStatusCode();
                        await SendMessage(new ErrorResponse
                        {
                            RequestId = command?.RequestId ?? 0,
                            Status = (int)statusCode,
                            Error = (int)statusCode < 500 ? exc.Message : statusCode.ToString()
                        });

                        if (statusCode == ResultStatusCodes.InternalError || !Global.IsAlpha)
                            logger.Error(exc);
                    }
                    reader = await webSocket.GetInputArray();
                }
            }
            catch (ConnectionCloseException e)
            {
                await CloseConnection(e.Status, e.Description);
            }
            catch (WebSocketException e)
            {
                var closureStatus = WebSocketCloseStatus.InternalServerError;
                if (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                    closureStatus = WebSocketCloseStatus.NormalClosure;
                else
                    logger.Error(e);
                await CloseConnection(closureStatus);
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
        }

        public async Task SendMessage(object message)
        {
            await webSocket.SendAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)), WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        public void Dispose()
        {
            webSocket?.Dispose();
            webSocket = null;
        }
    }
}
