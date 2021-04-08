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
using Centaurus.Xdr;

namespace Centaurus.Domain
{
    public class InfoWebSocketConnection : IDisposable
    {
        public InfoWebSocketConnection(AlphaContext context, WebSocket webSocket, string connectionId, string ip)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            this.webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            Ip = ip;
            ConnectionId = connectionId;
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            incomingBuffer = XdrBufferFactory.Rent(WebSocketExtension.ChunkSize);
        }

        static InfoCommandsHandlers commandHandlers = new InfoCommandsHandlers();

        static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly WebSocket webSocket;

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;

        private XdrBufferFactory.RentedBuffer incomingBuffer;

        public AlphaContext Context { get; }
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

        public async Task CloseConnection(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string desc = null)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await sendMessageSemaphore.WaitAsync();
                    try
                    {
                        var timeoutTokenSource = new CancellationTokenSource(1000);
                        await webSocket.CloseAsync(status, desc, timeoutTokenSource.Token);
                        cancellationTokenSource?.Cancel();
                    }
                    catch (WebSocketException exc)
                    {
                        //ignore client disconnection
                        if (exc.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely
                            && exc.WebSocketErrorCode != WebSocketError.InvalidState)
                            throw;
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        sendMessageSemaphore?.Release();
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Error on close connection");
            }
        }

        public event EventHandler OnClosed;

        public async Task Listen()
        {
            try
            {
                while (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted && !cancellationToken.IsCancellationRequested)
                {
                    var messageType = await webSocket.GetWebsocketBuffer(incomingBuffer, cancellationToken);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        //the client send close message
                        if (messageType == WebSocketMessageType.Close)
                        {
                            if (webSocket.State != WebSocketState.CloseReceived)
                                continue;
                            await sendMessageSemaphore.WaitAsync();
                            try
                            {
                                var timeoutTokenSource = new CancellationTokenSource(1000);
                                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Close", CancellationToken.None);
                            }
                            finally
                            {
                                sendMessageSemaphore.Release();
                                cancellationTokenSource?.Cancel();
                            }
                        }
                        else
                        {
                            BaseCommand command = null;
                            try
                            {
                                command = BaseCommand.Deserialize(incomingBuffer.AsSpan());
                                var handlerResult = await commandHandlers.HandleCommand(this, command);
                                await SendMessage(handlerResult);
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

                                if (statusCode == ResultStatusCodes.InternalError)
                                    logger.Error(exc);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                var closureStatus = WebSocketCloseStatus.InternalServerError;
                var desc = default(string);
                if (e is WebSocketException webSocketException
                    && webSocketException.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) //connection already closed by the other side
                    return;
                else if (e is ConnectionCloseException closeException)
                {
                    closureStatus = closeException.Status;
                    desc = closeException.Description;
                }
                else if (e is FormatException)
                    closureStatus = WebSocketCloseStatus.ProtocolError;
                else
                    logger.Error(e);
                await CloseConnection(closureStatus, desc);
            }
            finally
            {
                OnClosed?.Invoke(this, new EventArgs { });
            }
        }

        private SemaphoreSlim sendMessageSemaphore = new SemaphoreSlim(1);

        public async Task SendMessage(object message)
        {
            await sendMessageSemaphore.WaitAsync();
            try
            {
                await webSocket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            finally
            {
                sendMessageSemaphore.Release();
            }
        }

        public void Dispose()
        {
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
            sendMessageSemaphore?.Dispose();
            sendMessageSemaphore = null;

            if (locks != null)
            {
                foreach (var @lock in locks)
                {
                    @lock.Value.Dispose();
                }
                locks = null;
            }

            //make sure the socket is closed
            if (webSocket.State != WebSocketState.Closed)
                webSocket.Abort();

            incomingBuffer?.Dispose();
            incomingBuffer = null;
        }
    }
}
