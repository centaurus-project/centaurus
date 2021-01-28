using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class InfoWebSocketConnection : IDisposable
    {
        public InfoWebSocketConnection()
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }

        static InfoCommandsHandlers commandHandlers = new InfoCommandsHandlers();

        static Logger logger = LogManager.GetCurrentClassLogger();

        private WebSocket webSocket;

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;

        public string Ip { get; }
        public string ConnectionId { get; }

        private ConcurrentDictionary<BaseSubscription, DateTime> Subscriptions = new ConcurrentDictionary<BaseSubscription, DateTime>();
        private ConcurrentDictionary<BaseSubscription, SemaphoreSlim> locks = new ConcurrentDictionary<BaseSubscription, SemaphoreSlim>();

        public void AddSubscription(BaseSubscription baseSubscription)
        {
            Subscriptions.TryAdd(baseSubscription, default);
        }

        public List<BaseSubscription> GetSubscriptions()
        {
            return Subscriptions.Keys.ToList();
        }

        public void RemoveSubsctioption(BaseSubscription baseSubscription)
        {
            Subscriptions.TryRemove(baseSubscription, out _);
        }

        public async Task SendSubscriptionUpdate(BaseSubscription baseSubscription, SubscriptionUpdateBase update)
        {
            if (Subscriptions.TryGetValue(baseSubscription, out var lastUpdate))
            {
                var @lock = locks.GetOrAdd(baseSubscription, (_) => new SemaphoreSlim(1));
                await @lock.WaitAsync();
                try
                {
                    var updateOfInterest = update.GetUpdateForDate(lastUpdate);
                    if (updateOfInterest == null)
                        return;
                    Subscriptions[baseSubscription] = updateOfInterest.UpdateDate;
                    await SendMessage(updateOfInterest);
                }
                finally
                {
                    @lock.Release();
                }
            }
        }

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
                        cancellationTokenSource?.Cancel();
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
                while (true)
                {
                    using var message = await webSocket.GetWebsocketBuffer(cancellationToken);
                    BaseCommand command = null;
                    try
                    {
                        command = BaseCommand.Deserialize(message.AsSpan());
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
                if (e is OperationCanceledException)
                    return;
                logger.Error(e);
            }
        }

        public async Task SendMessage(object message)
        {
            await webSocket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public void Dispose()
        {
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            webSocket?.Dispose();
            webSocket = null;
            if (locks != null)
            {
                foreach (var @lock in locks)
                {
                    @lock.Value.Dispose();
                }
                locks = null;
            }
        }
    }
}
