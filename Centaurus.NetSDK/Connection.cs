using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Centaurus.Models;
using Centaurus.Xdr;
using NLog;
using static Centaurus.Xdr.XdrBufferFactory;

namespace Centaurus.NetSDK
{
    internal class Connection : IDisposable
    {
        public Connection(CentaurusClient client)
        {
            this.client = client;
            webSocketWrapper = Config.OutgoingConnectionFactory.GetConnection();
        }

        private readonly CentaurusClient client;
        public ConstellationConfig Config => client.Config;

        public ConstellationInfo ConstellationInfo { get; private set; }

        public bool IsConnected { get; private set; }

        public ulong AccountId { get; private set; }

        private readonly OutgoingConnectionWrapperBase webSocketWrapper;

        private readonly TaskCompletionSource<bool> HandshakeTask = new TaskCompletionSource<bool>();

        private WebSocket webSocket => webSocketWrapper?.WebSocket;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private MessageCollator collator = new MessageCollator();

        static Logger logger = LogManager.GetCurrentClassLogger();

        private const int RequestTimeout = 5000;

        public event Action<Exception> OnException;

        public event Action OnClose;

        private event Action<MessageEnvelopeBase> OnMessage;

        private RentedBuffer readerBuffer = Rent();

        private SemaphoreSlim sendMessageSemaphore = new SemaphoreSlim(1);

        public async Task Connect()
        {
            var wsUri = new Uri($"{(Config.UseSecureConnection ? "wss" : "ws")}://{Config.AlphaServerAddress}/centaurus");
            var query = HttpUtility.ParseQueryString(wsUri.Query);
            query.Set(WebSocketConstants.PubkeyParamName, Config.ClientKeyPair.AccountId);

            var uriBuilder = new UriBuilder(wsUri);
            uriBuilder.Query = query.ToString();

            wsUri = uriBuilder.Uri;

            //fetch constellation info from the server
            ConstellationInfo = await PublicApi.GetConstellationInfo(Config.AlphaServerAddress, Config.UseSecureConnection);
            //we expect that the first message from the server will start the handshake routine
            OnMessage += HandleHandshakeMessage;
            //connect the client websocket
            await webSocketWrapper.Connect(wsUri, cancellationTokenSource.Token);
            //listen for incoming messages
            _ = Task.Factory.StartNew(Listen, TaskCreationOptions.LongRunning).Unwrap();

            await HandshakeTask.Task;
        }

        /// <summary>
        /// Submit message to the constellation.
        /// </summary>
        /// <param name="envelope"></param>
        /// <returns></returns>
        public async Task<QuantumResult> SendMessage(MessageEnvelopeBase envelope)
        {
            if (!envelope.IsSignatureValid(Config.ClientKeyPair))
                envelope.Sign(Config.ClientKeyPair);

            QuantumResult result = null;
            if (envelope.Message.MessageId != default)
            {
                result = new QuantumResult(envelope, client);
                result.ScheduleExpiration(RequestTimeout);
                collator.Add(result);
            }

            using (var writerBuffer = Rent(1024))
            {
                try
                {
                    var writer = new XdrBufferWriter(writerBuffer.Buffer);
                    XdrConverter.Serialize(envelope, writer);
                    await sendMessageSemaphore.WaitAsync();
                    await webSocket.SendAsync(writerBuffer.Buffer.AsMemory(0, writer.Length),
                        WebSocketMessageType.Binary, true, cancellationTokenSource.Token);
                }
                catch (WebSocketException e)
                {
                    if (result != null)
                        result.SetException(e);

                    await CloseConnection(WebSocketCloseStatus.ProtocolError, e.Message);
                }
                catch (Exception e)
                {
                    if (result == null) throw;
                    result.SetException(e);
                }
                finally
                {
                    sendMessageSemaphore.Release();
                }
            }

            return result;
        }

        public async Task CloseConnection(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string statusDescription = "")
        {
            if (!IsConnected)
                return;
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await sendMessageSemaphore.WaitAsync();
                    try
                    {
                        await webSocket.CloseAsync(status, statusDescription, new CancellationTokenSource(1000).Token);
                        cancellationTokenSource.Cancel();
                    }
                    catch (WebSocketException e)
                    {
                        //ignore client disconnection
                        if (e.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely
                            && e.WebSocketErrorCode != WebSocketError.InvalidState)
                            throw;
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        sendMessageSemaphore.Release();
                        IsConnected = false;
                    }
                }
            }
            catch (WebSocketException e)
            {
                //ignore "closed-without-completing-handshake" error and invalid state error
                if (e.WebSocketErrorCode != WebSocketError.ConnectionClosedPrematurely
                    && e.WebSocketErrorCode != WebSocketError.InvalidState)
                    throw;
            }
            var closeException = new ConnectionCloseException(status, statusDescription);
            foreach (var pendingRequest in collator)
            {
                pendingRequest.SetException(closeException);
            }

            if (status != WebSocketCloseStatus.NormalClosure)
            {
                _ = Task.Factory.StartNew(() => OnException?.Invoke(closeException));
            }
        }

        public void Dispose()
        {
            CloseConnection().Wait();
            cancellationTokenSource.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;

            //make sure the socket is closed
            if (webSocket.State != WebSocketState.Closed)
                webSocket.Abort();
            webSocket.Dispose();

            OnMessage -= HandleHandshakeMessage;
            OnMessage -= HandleMessage;

            readerBuffer?.Dispose();
            readerBuffer = null;
        }

        private async Task Listen()
        {
            try
            {
                while (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted && !cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var messageType = await webSocket.GetWebsocketBuffer(readerBuffer, cancellationTokenSource.Token);
                    if (cancellationTokenSource.Token.IsCancellationRequested) break;
                    //the client send close message
                    if (messageType == WebSocketMessageType.Close)
                    {
                        if (webSocket.State != WebSocketState.CloseReceived) continue;

                        await sendMessageSemaphore.WaitAsync();
                        try
                        {
                            await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Close", CancellationToken.None);
                        }
                        finally
                        {
                            sendMessageSemaphore.Release();
                            cancellationTokenSource.Cancel();
                        }
                        continue;
                    }
                    try
                    {
                        var envelope = XdrConverter.Deserialize<MessageEnvelopeBase>(new XdrBufferReader(readerBuffer.Buffer));
                        OnMessage?.Invoke(envelope);
                    }
                    catch
                    {
                        OnException?.Invoke(new UnexpectedMessageException("Failed to deserialize a response message received from the server."));
                    }
                }
            }
            catch (OperationCanceledException)
            { }
            catch (Exception e)
            {
                var status = WebSocketCloseStatus.InternalServerError;
                //connection has been already closed by the other side
                if ((e as WebSocketException)?.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) return;

                string errorDescription = null;
                if (e is ConnectionCloseException closeException)
                {
                    status = closeException.Status;
                    errorDescription = closeException.Description;
                }
                else if (e is FormatException)
                {
                    status = WebSocketCloseStatus.ProtocolError;
                }
                else
                {
                    logger.Error(e);
                }

                await CloseConnection(status, errorDescription);
            }
            finally
            {
                _ = Task.Factory.StartNew(() => OnClose?.Invoke());
            }
        }

        /// <summary>
        /// Handle server quantum response messages.
        /// </summary>
        /// <param name="envelope"></param>
        private void HandleMessage(MessageEnvelopeBase envelope)
        {
            if (!collator.Resolve(envelope) && envelope.Message is IQuantumInfoContainer quantum)
                client.HandleQuantumResult(quantum);
        }

        /// <summary>
        /// Handle handshake initialization routine messages.
        /// </summary>
        /// <param name="envelope"></param>
        private void HandleHandshakeMessage(MessageEnvelopeBase envelope)
        {
            if (!(envelope.Message is HandshakeRequest handshakeRequest))
            {
                logger.Trace("Server sent unknown message before the handshake initialization.");
                return;
            }

            //change message handler after handshake received
            OnMessage -= HandleHandshakeMessage;
            OnMessage += HandleMessage;

            //start new task to avoid deadlock 
            Task.Factory.StartNew(async () =>
            {
                logger.Trace("Server initialized handshake routine.");
                try
                {

                    var result = await SendMessage(new HandshakeResponse { HandshakeData = handshakeRequest.HandshakeData }.CreateEnvelope());
                    logger.Trace("Handshake confirmation message sent.");

                    await result.OnAcknowledged;

                    var handshakeInitResult = result.Result.Message as ClientConnectionSuccess;
                    if (handshakeInitResult.Status != ResultStatusCode.Success)
                        throw new Exception("Server rejected handshake confirmation.");
                    AccountId = handshakeInitResult.AccountId;
                    logger.Trace("Handshake routine finalized successfully.");
                    HandshakeTask.TrySetResult(true);
                    IsConnected = true;
                }
                catch (Exception e)
                {
                    HandshakeTask.TrySetException(e);
                    logger.Error(e);
                    logger.Trace("Failed to perform handshake routine.");
                }
            });
        }
    }
}
